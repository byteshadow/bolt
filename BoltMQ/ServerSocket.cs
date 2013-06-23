using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public class ServerSocket : AsyncServerSocket
    {
        private bool _disposed;

        public ServerSocket()
            : this(8192, DefaultNumOfConnections)
        {
        }

        public ServerSocket(int bufferSize, int maxConnections)
        {
            ReceiveBufferSize = bufferSize;
            MaxConnections = maxConnections;
            SocketType = SocketType.Stream;
            ProtocolType = ProtocolType.Tcp;
            Initialize(new MessageProcessor(new MessageBroker(), new Serializer()));
            StreamHandlers = new List<IStreamHandler>(maxConnections);
        }

        protected override sealed void Initialize(IMessageProcessor messageProcessor)
        {
            base.Initialize(messageProcessor);
        }

        protected override ISession SessionFactory(Socket socket)
        {
            var sessionId = Guid.NewGuid();

            Session session = new Session(StreamHandlerFactory(sessionId), socket, sessionId);

            session.OnDisconnected += SessionDisconnected;

            return session;
        }

        private void SessionDisconnected(object sender, ISession e)
        {
            e.OnDisconnected -= SessionDisconnected;

            SessionClosed(sender, e);

            var streamHandler = StreamHandlers.FirstOrDefault(s => s.SessionId == e.SessionId);
            if (streamHandler != null)
            {
                streamHandler.Dispose();
                StreamHandlers.Remove(streamHandler);
            }
        }

        protected override IStreamHandler StreamHandlerFactory(Guid sessionId)
        {
            var streamHandler = new StreamHandler(MessageProcessor, sessionId);
            //var streamHandler = new BlockingStreamHandler(MessageProcessor, sessionId);
            StreamHandlers.Add(streamHandler);
            return streamHandler;
        }

        public List<IStreamHandler> StreamHandlers { get; private set; }

        public override void SendAsync<T>(T message)
        {
            foreach (KeyValuePair<Guid, ISession> activeConnection in ActiveSessions)
            {
                MessageProcessor.SendAsync(message, activeConnection.Value);
            }
        }

        public override void SendAsync<T>(T message, Guid sessionId)
        {
            ISession session;
            if (ActiveSessions.TryGetValue(sessionId, out session))
            {
                MessageProcessor.SendAsync(message, session);
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool dispose)
        {
            if (_disposed || !dispose) return;

            Close();

            _disposed = true;
        }

        ~ServerSocket()
        {
            Dispose(!_disposed);
        }

       
    }
}