using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public class ServerSocket : AsyncServerSocket
    {
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

        protected override IStreamHandler StreamHandlerFactory(Guid sessionId)
        {
            var streamHandler = new StreamHandler(MessageProcessor, sessionId);
            
            StreamHandlers.Add(streamHandler);

            return streamHandler;
        }

        protected override ISession SessionFactory(Socket socket)
        {
            Guid sessionId = Guid.NewGuid();

            IStreamHandler streamHandler = StreamHandlerFactory(sessionId);

            Session session = new Session(streamHandler, socket, sessionId);

            return session;
        }

        protected override void OnSessionDisconnected(object sender, ISession e)
        {
            base.OnSessionDisconnected(sender, e);

            var streamHandler = StreamHandlers.FirstOrDefault(s => s.SessionId == e.SessionId);

            if (streamHandler != null)
            {
                streamHandler.Dispose();
                StreamHandlers.Remove(streamHandler);
            }
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

        public override void OnDispose()
        {
            Close();
        }       
    }
}