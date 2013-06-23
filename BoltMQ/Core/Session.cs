using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Threading;
using BoltMQ.Core.Collection;
using BoltMQ.Core.Interfaces;
using System.Reactive.Linq;
namespace BoltMQ.Core
{
    public class Session : ISession
    {
        public event EventHandler<ISession> OnDisconnected;

        private bool _disposed;
        private volatile bool _isSending = false;

        private readonly SocketAsyncEventArgs _receiveEventArgs;
        private readonly SocketAsyncEventArgs _sendEventArgs;

        private readonly BytesRingBuffer _outgoingBuffer;

        private readonly object _syncSendObject = new object();
        readonly AutoResetEvent _freeSpaceHandler = new AutoResetEvent(false);

        private IDisposable _receiveSubscription;

        private readonly IObservable<SocketAsyncEventArgs> _sendObservable;
        private readonly IDisposable _sendSubscribtion;

        public IStreamHandler StreamHandler { get; private set; }
        public SocketAsyncEventArgs ReceiveEventArgs { get { return _receiveEventArgs; } }
        public SocketAsyncEventArgs SendEventArgs { get { return _sendEventArgs; } }
        public Guid SessionId { get; private set; }
        public bool IsConnected
        {
            get { return Socket != null && Socket.Connected; }
        }
        public Socket Socket { get; private set; }

        public Session(IStreamHandler streamHandler, Socket socket, Guid sessionId)
        {
            SessionId = sessionId;
            Socket = socket;
            StreamHandler = streamHandler;

            _outgoingBuffer = new BytesRingBuffer(64 * 1024);

            _receiveEventArgs = new SocketAsyncEventArgs { UserToken = this };

            _sendEventArgs = new SocketAsyncEventArgs { UserToken = this, RemoteEndPoint = socket.RemoteEndPoint };
            

            _sendObservable = _sendEventArgs.ToObservable();
            _sendSubscribtion = _sendObservable.SubscribeOn(ThreadPoolScheduler.Instance).Subscribe(OnSendCompleted);
        }

        //#region Receive
        //private void OnReceiveCompleted(SocketAsyncEventArgs args)
        //{
        //    if (args.BytesTransferred == 0)
        //    {
        //        Close(this);
        //    }
        //    else if (args.SocketError != SocketError.Success)
        //    {
        //        ProcessError(args);
        //    }
        //    else
        //    {
        //        ISession session = (ISession)args.UserToken;
        //        var success = session.StreamHandler.ParseStream(args.Buffer, args.Offset, args.BytesTransferred);
        //        if (success)
        //            ReceiveAsync(args);
        //        else
        //        {
        //            Close(this);
        //        }
        //    }
        //}

        //public void ReceiveAsync(SocketAsyncEventArgs args)
        //{
        //    ISession session = (ISession)args.UserToken;
        //    try
        //    {
        //        bool willRaiseEvent = session.Socket.ReceiveAsync(args);
        //        if (!willRaiseEvent)
        //        {
        //            OnReceiveCompleted(args);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(ex.ToString());
        //        ProcessError(args);
        //    }
        //}
        //#endregion

        #region Send
        /// <summary>
        /// This will block the current thread untill the data has been sent or an error is reported
        /// </summary>
        /// <param name="data">Data to send to the remote endpoint</param>
        /// <returns></returns>
        public int Send(byte[] data)
        {
            lock (_syncSendObject)
            {
                if (IsConnected)
                {
                    return Socket.Send(data);
                }
                
                Trace.TraceError("Socket is closed. {0}", Socket.RemoteEndPoint);
                throw new SocketException((int)SocketError.NotConnected);
            }
        }

        public void SendAsync(byte[] data)
        {
            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            if (_outgoingBuffer.FreeBytes >= data.Length)
            {
                WriteToSendBuffer(data);
            }
            else
            {
                _outgoingBuffer.NotifyFreeSpace(data.Length, _freeSpaceHandler);

                if (!_freeSpaceHandler.WaitOne(5000) && _outgoingBuffer.FreeBytes < data.Length)
                {
                    //Slow consumer
                    Trace.TraceError("Slow consumer detected. Closing Socket to {0}.", Socket.RemoteEndPoint);
                    Close();
                }
                else
                {
                    WriteToSendBuffer(data);
                }
            }
        }

        private void WriteToSendBuffer(byte[] data)
        {
            if (!_outgoingBuffer.Write(data, 0, data.Length))
                Trace.TraceError("Ensure there is only one Buffer writer at any given time.");

            if (!_isSending)
                FlushSendBuffer();
        }

        public void FlushSendBuffer()
        {
            lock (_syncSendObject)
            {
                int bytesRead;
                var buffer = _outgoingBuffer.ReadAll(out bytesRead);

                if (bytesRead == 0)
                {
                    _isSending = false;
                    return;
                }

                _sendEventArgs.SetBuffer(buffer, 0, bytesRead);
                _isSending = true;

                bool willRaiseEvent = Socket.SendAsync(_sendEventArgs);

                if (!willRaiseEvent)
                    OnSendCompleted(_sendEventArgs);
            }
        }

        public void SetReceiveDisposable(IDisposable disposable)
        {
            _receiveSubscription = disposable;
        }

        private void OnSendCompleted(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                _outgoingBuffer.Release(e.Buffer);

                ISession session = (ISession)e.UserToken;

                //Send any pending messages
                session.FlushSendBuffer();
            }
            else
            {
                ProcessError(e);
            }
        }
        #endregion

        private void ProcessError(SocketAsyncEventArgs args)
        {
            ISession session = args.UserToken as ISession;

            if (session != null && session.StreamHandler != null && session.StreamHandler.StreamHandlerException != null)
            {
                Trace.TraceError(session.StreamHandler.StreamHandlerException.ToString());
            }

            if (session != null)
                Close();
        }

        public void Close()
        {
            if (Socket == null) return;

            try
            {
                Socket.Shutdown(SocketShutdown.Send);
            }
            catch
            {
                Trace.TraceWarning("Socket has already shutdown.");
            }
            finally
            {
                Socket.Close();

                var local = OnDisconnected;

                if (local != null)
                    local(null, this);
            }
        }

        #region Cleanup
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing) return;

            _receiveSubscription.Dispose();
            _sendSubscribtion.Dispose();

            _disposed = true;
        }
        ~Session()
        {
            Dispose(false);
        }
        #endregion
    }
}