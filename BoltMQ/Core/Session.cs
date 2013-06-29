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
    public class Session : Disposable, ISession
    {
        public event EventHandler<ISession> OnDisconnected;
        public event EventHandler<byte[]> OnSendAsync;

        private volatile bool _isSending;
        private readonly BytesRingBuffer _outgoingBuffer;
        private readonly SocketAsyncEventArgs _sendEventArgs;
        private readonly SocketAsyncEventArgs _receiveEventArgs;

        private IDisposable _receiveSubscription;
        private readonly IDisposable _sendSubscription;
        private readonly IObservable<SocketAsyncEventArgs> _sendObservable;

        private readonly object _syncSendObject = new object();
        private readonly AutoResetEvent _freeSpaceHandler = new AutoResetEvent(false);

        #region Properties
        public IStreamHandler StreamHandler { get; private set; }
        public SocketAsyncEventArgs ReceiveEventArgs { get { return _receiveEventArgs; } }
        public SocketAsyncEventArgs SendEventArgs { get { return _sendEventArgs; } }
        public Guid SessionId { get; private set; }
        public bool IsConnected
        {
            get { return Socket != null && Socket.Connected; }
        }
        public Socket Socket { get; private set; }
        #endregion

        public Session(IStreamHandler streamHandler, Socket socket, Guid sessionId)
        {
            SessionId = sessionId;
            Socket = socket;
            StreamHandler = streamHandler;

            _outgoingBuffer = new BytesRingBuffer(64 * 1024);
            _receiveEventArgs = new SocketAsyncEventArgs { UserToken = this };
            _sendEventArgs = new SocketAsyncEventArgs { UserToken = this, RemoteEndPoint = socket.RemoteEndPoint };

            _sendObservable = _sendEventArgs.ToObservable();
            _sendSubscription = _sendObservable.SubscribeOn(ThreadPoolScheduler.Instance).Subscribe(OnSendCompleted);

            IObservable<byte[]> dataToSend = Observable.FromEventPattern<byte[]>(this, "OnSendAsync").Select(pattern => pattern.EventArgs);

            dataToSend.Subscribe(data =>
                {
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
                });

        }

        #region Send
        //TODO: THIS IS A HACK!!! REFACTOR THE WHOLE OF SEND/SENDASYNC
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

            var local = OnSendAsync;
            if (local != null)
            {
                local(null, data);
            }
            else
            {
                Send(data);
            }
        }

        private int _pendingMsgs;

        private void WriteToSendBuffer(byte[] data)
        {
            if (!_outgoingBuffer.Write(data, 0, data.Length))
            {
                Trace.TraceError("Ensure there is only one Buffer writer at any given time.");
            }
            else
            {
                Interlocked.Increment(ref _pendingMsgs);

                if (!_isSending)
                    FlushSendBuffer();
            }
        }

        public void FlushSendBuffer()
        {
            lock (_syncSendObject)
            {
                int bytesRead;

                int preReadMsgs = Interlocked.Exchange(ref _pendingMsgs, 0);
                byte[] buffer = _outgoingBuffer.ReadAll(out bytesRead);
                int postReadMsgs = Interlocked.Exchange(ref _pendingMsgs, 0);

                if (bytesRead == 0 && preReadMsgs == postReadMsgs && preReadMsgs == 0)
                {
                    _isSending = false;
                    return;
                }
                if (bytesRead == 0 && postReadMsgs != preReadMsgs)
                {
                    buffer = _outgoingBuffer.ReadAll(out bytesRead);

                    if (bytesRead == 0)
                    {
                        _isSending = false;
                        return;
                    }
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

        public void OnReceiveCompleted(Action<SocketAsyncEventArgs> onReceiveCompleted)
        {
            //Observe the Receive Event Arg for incoming messages
            IObservable<SocketAsyncEventArgs> receiveObservable = ReceiveEventArgs.ToObservable();

            //Setup the subscription for the Receive Event
            _receiveSubscription = receiveObservable.SubscribeOn(ThreadPoolScheduler.Instance).Subscribe(onReceiveCompleted);
        }

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

        public override void OnDispose()
        {
            if (_receiveSubscription != null)
                _receiveSubscription.Dispose();

            if (_sendSubscription != null)
                _sendSubscription.Dispose();
        }
    }
}