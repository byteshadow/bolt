using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BoltMQ.Core.Interfaces;

namespace BoltMQ.Core
{
    public abstract class AsyncSocket : Disposable, IAsyncSocket
    {
        public const int DefaultNumOfConnections = 10;
        public const int DefaultBufferSize = 8192;

        protected BufferPool _receiveBufferPool;

        #region Properties
        /// <summary>
        /// The buffer size allocated to each socket
        /// </summary>
        public int ReceiveBufferSize { get; set; }
        /// <summary>
        /// The port number the socket is bound to
        /// </summary>
        public int Port { get; set; }
        public AddressFamily AddressFamily { get; set; }
        public SocketType SocketType { get; set; }
        public ProtocolType ProtocolType { get; set; }
        public Socket Socket { get; protected set; }
        public IMessageProcessor MessageProcessor { get; protected set; }
        protected int SendBufferSize { get; set; }
        #endregion

        #region abstract methods
        /// <summary>
        /// This returns a NEW <see cref="IStreamHandler"/> object
        /// </summary>
        /// <param name="sessionId"> </param>
        /// <returns><see cref="IStreamHandler"/></returns>
        protected abstract IStreamHandler StreamHandlerFactory(Guid sessionId);
        /// <summary>
        /// This Session factory returns a NEW ISession object when called
        /// </summary>
        /// <returns> a new <see cref="ISession"/> object</returns>
        protected abstract ISession SessionFactory(Socket socket);
        public abstract void Close();
        public abstract void SendAsync<T>(T message);
        protected virtual void Initialize(IMessageProcessor messageProcessor)
        {
            MessageProcessor = messageProcessor;
        }
        #endregion

        /// <summary>
        /// Allows code to subscribe to message type received by the socket
        /// </summary>
        /// <typeparam name="T">The type of the object expected to received by the socket</typeparam>
        /// <param name="messageHandler">The handler that is interested in the message</param>
        public void AddMessageHandler<T>(EventHandler<BoltEventArgs<T>> messageHandler)
        {
            //pass the handler down to the MessageBroker
            MessageProcessor.MessageBroker.Subscribe(messageHandler);
        }

        /// <summary>
        /// Allows code to unsubscribe from the socket for the given message type
        /// </summary>
        /// <typeparam name="T">The type to unsbscribe from</typeparam>
        /// <param name="messageHandler">The handler to remove</param>
        public void RemoveMessageHandler<T>(EventHandler<BoltEventArgs<T>> messageHandler)
        {
            //pass the handler down to the MessageBroker
            MessageProcessor.MessageBroker.Unsubscribe(messageHandler);
        }

        protected static IPEndPoint GetIPEndPoint(Uri uri)
        {
            var addresses = Dns.GetHostAddresses(uri.Host);

            if (addresses.Length == 0)
            {
                throw new ArgumentException("Unable to retrieve address from specified host name.", "uri");
            }
            else if (addresses.Length > 1)
            {
                StringBuilder sb = new StringBuilder();
                foreach (IPAddress ipAddress in addresses)
                {
                    sb.AppendLine(ipAddress.ToString());
                }

                throw new ArgumentException(
                    string.Format("There is more that one IP address to the specified host \"{0}\".{1}{2}",
                                  uri.Host, Environment.NewLine, sb), "uri");
            }

            return new IPEndPoint(addresses[0], uri.Port);
        }

        protected void SetupBufferPools(int maxConnections)
        {
            //if (ReceiveBufferSize < DefaultBufferSize)
            //    ReceiveBufferSize = DefaultBufferSize;

            if (SendBufferSize < DefaultBufferSize)
                SendBufferSize = DefaultBufferSize;

            //var mtu = GetNetworkInterfaceMtu(ipEndPoint);

            //Smaller buffer helps in detecting slow consumers
            Socket.SendBufferSize = SendBufferSize;
            //Do not set the ReceiveBufferSize leave as default
            //Socket.ReceiveBufferSize = <Should be Big: DO NOT SET>;

            //Setup the buffer
            //The receive buffer size for each client should be same as the Socket.ReceiveBufferSize
            _receiveBufferPool = new BufferPool(maxConnections, Socket.ReceiveBufferSize);
        }

        protected void SetupSession(ISession session)
        {
            session.OnReceiveCompleted(OnReceiveCompleted);

            // assign a byte buffer from the buffer pool to the SocketAsyncEventArg objects
            _receiveBufferPool.SetBuffer(session.ReceiveEventArgs);

            //Subscribe to the disconnect event
            session.OnDisconnected += OnSessionDisconnected;
        }

        protected virtual void OnSessionDisconnected(object sender, ISession e)
        {
            e.OnDisconnected -= OnSessionDisconnected;
        }

        private void OnReceiveCompleted(SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred == 0)
            {
                ISession session = (ISession)args.UserToken;
                session.Close();
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
                    session.Close();
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
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                ProcessError(args);
            }
        }

        private static void ProcessError(SocketAsyncEventArgs args)
        {
            ISession session = args.UserToken as ISession;

            if (session != null && session.StreamHandler != null && session.StreamHandler.StreamHandlerException != null)
            {
                Trace.TraceError(session.StreamHandler.StreamHandlerException.ToString());
            }

            if (session != null)
                session.Close();
        }
    }
}