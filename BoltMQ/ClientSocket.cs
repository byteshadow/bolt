using System;
using System.Net.Sockets;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public sealed class ClientSocket : AsyncClientSocket
    {
        private bool _disposed;

        public ClientSocket()
            : this(new MessageBroker(), new Serializer()) { }

        public ClientSocket(IMessageBroker messageBroker, ISerializer serializer)
            : this(new MessageProcessor(messageBroker, serializer)) { }

        public ClientSocket(IMessageProcessor messageProcessor)
        {
            SocketType = SocketType.Stream;
            ProtocolType = ProtocolType.Tcp;

            Initialize(messageProcessor);
        }

        #region Overrides of AsyncSocket

        public IStreamHandler StreamHandler { get; private set; }

        protected override IStreamHandler StreamHandlerFactory(Guid sessionId)
        {
            return StreamHandler ?? (StreamHandler = new StreamHandler(MessageProcessor, sessionId));
        }

        protected override ISession SessionFactory(Socket socket)
        {
            var sessionId = Guid.NewGuid();
            return new Session(StreamHandlerFactory(sessionId),socket, sessionId);
        }

        public override void Close()
        {
            if (!Connected) return;

            Session.Close(Session);

            Socket.Close();
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

        ~ClientSocket()
        {
            Dispose(false);
        }
        #endregion
    }
}
