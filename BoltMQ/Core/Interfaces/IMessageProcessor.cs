using System;
using System.Collections.Generic;

namespace BoltMQ.Core.Interfaces
{
    public interface IMessageProcessor : IDisposable
    {
        ISerializer Serializer { get; }
        IMessageBroker MessageBroker { get; }

        void Process(byte[] message, Guid sessionId);
        void Process(IEnumerable<byte[]> messages, Guid sessionId);

        void Send<T>(T message, ISession activeSession);
        void SendAsync<T>(T message, ISession session);
    }
}