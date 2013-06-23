using System;
using System.Net.Sockets;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public sealed class ClientSocket : AsyncClientSocket
    {
        public IStreamHandler StreamHandler { get; private set; }

        public ClientSocket() : this(new MessageBroker(), new Serializer()) { }

        public ClientSocket(IMessageBroker messageBroker, ISerializer serializer)
            : this(new MessageProcessor(messageBroker, serializer)) { }

        public ClientSocket(IMessageProcessor messageProcessor)
        {
            SocketType = SocketType.Stream;
            ProtocolType = ProtocolType.Tcp;
            Initialize(messageProcessor);
        }

        #region Overrides of AsyncSocket
        protected override IStreamHandler StreamHandlerFactory(Guid sessionId)
        {
            return StreamHandler ?? (StreamHandler = new StreamHandler(MessageProcessor, sessionId));
        }

        protected override ISession SessionFactory(Socket socket)
        {
            Guid sessionId = Guid.NewGuid();
            IStreamHandler streamHandler = StreamHandlerFactory(sessionId);
            Session session = new Session(streamHandler, socket, sessionId);
            return session;
        }

        public override void Close()
        {
            if (!Connected) return;

            Session.Close();
        }

        public override void OnDispose()
        {
            Close();
        }
        #endregion
    }
}
