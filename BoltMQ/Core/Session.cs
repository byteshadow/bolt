using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly BytesRingBuffer _bytesRingBuffer;

        private readonly object _syncSendObject = new object();
        readonly AutoResetEvent _freeSpaceHandler = new AutoResetEvent(false);

        private readonly IObservable<SocketAsyncEventArgs> _receiveObservable;
        private readonly IDisposable _receiveSubscription;

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
            _receiveEventArgs = new SocketAsyncEventArgs { UserToken = this };
            _sendEventArgs = new SocketAsyncEventArgs { UserToken = this, RemoteEndPoint = socket.RemoteEndPoint };

            _bytesRingBuffer = new BytesRingBuffer(64 * 1024);

            _receiveObservable = Observable.FromEventPattern<SocketAsyncEventArgs>(_receiveEventArgs, "Completed").Select(pattern => pattern.EventArgs);
            _receiveSubscription = _receiveObservable.SubscribeOn(Scheduler.Default).Subscribe(OnReceiveCompleted);

            _sendObservable = Observable.FromEventPattern<SocketAsyncEventArgs>(_sendEventArgs, "Completed").Select(pattern => pattern.EventArgs);
            _sendSubscribtion = _sendObservable.Subscribe(OnSendCompleted);
        }

        private void OnReceiveCompleted(SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred == 0)
            {
                Close(this);
            }
            else if (args.SocketError != SocketError.Success)
            {
                ProcessError(args);
            }
            else
            {
                ISession session = (ISession)args.UserToken;
                var success = session.StreamHandler.ParseStream(args.Buffer, args.Offset, args.BytesTransferred);
                if (success)
                    ReceiveAsync(args);
                else
                {
                    Close(this);
                }
            }
        }

        public void ReceiveAsync(SocketAsyncEventArgs args)
        {
            ISession session = (ISession)args.UserToken;
            try
            {
                bool willRaiseEvent = session.Socket.ReceiveAsync(args);
                if (!willRaiseEvent)
                {
                    OnReceiveCompleted(args);
                }
            }
            catch (Exception)
            {
                ProcessError(args);
            }
        }


        #region Send
        /// <summary>
        /// Blocking Send call
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
                else
                {
                    throw new SocketException((int)SocketError.NotConnected);
                }
            }
        }

        public void SendAsync(byte[] data)
        {
            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            if (_bytesRingBuffer.FreeBytes >= data.Length)
            {
                WriteToSendBuffer(data);
            }
            else
            {
                _bytesRingBuffer.NotifyFreeSpace(data.Length, _freeSpaceHandler);

                if (!_freeSpaceHandler.WaitOne(5000) && _bytesRingBuffer.FreeBytes < data.Length)
                {
                    //Slow consumer
                    Trace.TraceError("Slow consumer detected. Closing Socket to {0}.", Socket.RemoteEndPoint);
                    Close(this);
                }
                else
                {
                    WriteToSendBuffer(data);
                }
            }
        }

        private void WriteToSendBuffer(byte[] data)
        {
            if (!_bytesRingBuffer.Write(data, 0, data.Length))
                Trace.TraceError("Ensure there is only one Buffer writer at any given time.");

            if (!_isSending)
                SendAsync();
        }

        private void SendAsync()
        {
            lock (_syncSendObject)
            {
                int bytesRead;
                var buffer = _bytesRingBuffer.ReadAll(out bytesRead);

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

        private void OnSendCompleted(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                _bytesRingBuffer.Release(e.Buffer);
                //Send any pending messages
                SendAsync();
            }
            else
            {
                Close(this);
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
                Close(session);
        }

        public void Close(ISession session)
        {
            if (session == null || session.Socket == null) return;

            var socket = session.Socket;

            try
            {
                socket.Shutdown(SocketShutdown.Send);
            }
            catch
            {
                Trace.TraceWarning("Socket has already shutdown.");
            }
            finally
            {
                socket.Close();

                var local = OnDisconnected;

                if (local != null)
                    local(null, session);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing) return;

            _receiveSubscription.Dispose();
            _sendSubscribtion.Dispose();

            _disposed = true;
        }
    }
}