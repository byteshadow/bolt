using System;

namespace BoltMQ.Core.Interfaces
{
    public interface IMessageBroker
    {
        void Subscribe<T>(EventHandler<BoltEventArgs<T>> messageHandler);
        void Unsubscribe<T>(EventHandler<BoltEventArgs<T>> messageHandler);
        void Broadcast(BoltEventArgs<object> args);
    }
}