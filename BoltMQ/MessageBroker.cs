using System;
using System.Collections.Concurrent;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public sealed class MessageBroker : IMessageBroker
    {
        private readonly ConcurrentDictionary<Type, ISubscribtion> _subscribtions = new ConcurrentDictionary<Type, ISubscribtion>();

        public void Subscribe<T>(EventHandler<BoltEventArgs<T>> messageHandler)
        {
            if (messageHandler == null)
                return;

            var type = typeof(T);

            // Use GetOrAdd to avoid race conditions when multiple threads
            // attempt to register handlers for the same message type.
            var subscription = (Subscription<T>)_subscribtions.GetOrAdd(
                type, _ => new Subscription<T>());

            subscription.MessageReceived += messageHandler;
        }

        public void Unsubscribe<T>(EventHandler<BoltEventArgs<T>> messageHandler)
        {
            if (messageHandler == null)
                return;

            var type = typeof(T);

            if (_subscribtions.ContainsKey(type))
            {
                var subscribtion = _subscribtions[type] as Subscription<T>;

                if (subscribtion != null)
                    subscribtion.MessageReceived -= messageHandler;
            }
        }

        public void Broadcast(BoltEventArgs<object> args)
        {
            if (Equals(args, null) || Equals(args.Message, null))
                return;

            Type type = args.Message.GetType();

            if (_subscribtions.ContainsKey(type))
            {
                _subscribtions[type].BroadcastMessage(args);
            }
            else
            {
                foreach (Type keyType in _subscribtions.Keys)
                {
                    if (keyType.IsAssignableFrom(type))
                        _subscribtions[keyType].BroadcastMessage(args);
                }
            }
        }
    }
}