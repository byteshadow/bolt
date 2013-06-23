using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public class MessageProcessor : IMessageProcessor
    {
        private readonly IMessageBroker _messageBroker;
        private readonly ISerializer _serializer;
        private bool _disposed;

        private delegate void MessagesDelegate(IEnumerable<byte[]> messages, Guid sessionId);
        private event MessagesDelegate MessagesHandler;

        public MessageProcessor(IMessageBroker messageBroker, ISerializer serializer)
        {
            _messageBroker = messageBroker;
            _serializer = serializer;
            MessagesHandler += OnMessagesReceived;
        }

        void OnMessagesReceived(IEnumerable<byte[]> messages, Guid sessionId)
        {
            var msgs = messages.ToArray();
            for (int i = 0; i < msgs.Length; i++)
            {
                Process(msgs[i], sessionId);
            }
        }

        public ISerializer Serializer
        {
            get { return _serializer; }
        }

        public IMessageBroker MessageBroker
        {
            get { return _messageBroker; }
        }

        public void Process(byte[] data, Guid sessionId)
        {
            var entity = _serializer.Deserialize(data);

            if (entity != null)
            {
                var args = new BoltEventArgs<object>(sessionId, entity);
                _messageBroker.Broadcast(args);
            }
        }

        public void Process(IEnumerable<byte[]> messages, Guid sessionId)
        {
            if (!_disposed)
                MessagesHandler(messages, sessionId);
        }

        public void Send<T>(T message, ISession activeSession)
        {
            byte[] byteMessage = _serializer.Serialize(message);

            if (!Equals(message, null))
                activeSession.Send(byteMessage);
        }

        public void SendAsync<T>(T message, ISession activeSession)
        {
            byte[] byteMessage = _serializer.Serialize(message);

            if (!Equals(message, null))
                activeSession.SendAsync(byteMessage);
        }
        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool dispose)
        {
            if (_disposed || !dispose)
            {
                return;
            }
            MessagesHandler -= OnMessagesReceived;
            _disposed = true;
        }
        ~MessageProcessor()
        {
            Dispose(false);
        }

        #endregion
    }
}