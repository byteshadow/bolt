using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BoltMQ.Core.Interfaces;

namespace BoltMQ.Core
{
    public abstract class AsyncClientSocket : AsyncSocket, IAsyncClientSocket
    {
        private ISession _session;
        private IPEndPoint _remoteIPEndPoint;
        private SocketAsyncEventArgs _connectEventArgs;
        private readonly AutoResetEvent _connectResetEvent = new AutoResetEvent(false);

        public IPEndPoint RemoteIPEndPoint { get { return _remoteIPEndPoint; } }
        public ISession Session { get { return _session; } }
        public bool Connected { get; set; }

        public void Connect(string uri)
        {
            var ipEndpoint = GetIPEndPoint(new Uri(uri));
            Connect(ipEndpoint);
        }

        /// <summary>
        /// Connect to a host
        /// </summary>
        /// <param name="ipEndPoint"></param>
        public void Connect(IPEndPoint ipEndPoint)
        {
            _remoteIPEndPoint = ipEndPoint;

            Socket = new Socket(ipEndPoint.AddressFamily, SocketType, ProtocolType);

            _connectEventArgs = new SocketAsyncEventArgs { RemoteEndPoint = _remoteIPEndPoint };
            _connectEventArgs.Completed += OnConnectCompleted;

            SetupBufferPools(1);

            bool willRaiseEvent = Socket.ConnectAsync(_connectEventArgs);

            if (!willRaiseEvent)
            {
                ProcessConnect();
            }

            if (!_connectResetEvent.WaitOne(60000))
            {
                throw new TimeoutException(string.Format("Failed to connect to {0} within {1} seconds.", ipEndPoint, 10));
            }
        }

        private void OnConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(e == _connectEventArgs);

            if (e.SocketError == SocketError.Success)
                ProcessConnect();
            else
            {
                throw new SocketException((Int32)e.SocketError);
            }
        }

        private void ProcessConnect()
        {
            _session = SessionFactory(_connectEventArgs.ConnectSocket);
            _receiveBufferPool.SetBuffer(_session.ReceiveEventArgs);
            _session.OnDisconnected += SessionDisconnected;

            Connected = true;

            _session.ReceiveAsync(_session.ReceiveEventArgs);
            _connectResetEvent.Set();
        }

        private void SessionDisconnected(object sender, ISession session)
        {
            session.OnDisconnected -= SessionDisconnected;

            Connected = false;

            if (session.SendEventArgs.SocketError == SocketError.ConnectionAborted)
                Connect(_remoteIPEndPoint);
        }

        public void Send<T>(T message)
        {
            MessageProcessor.Send(message, _session);
        }
        public override void SendAsync<T>(T message)
        {
            MessageProcessor.SendAsync(message, _session);
        }
    }
}