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
            Subscription<T> subscription;

            if (!_subscribtions.ContainsKey(type))
            {
                subscription = new Subscription<T>();
                _subscribtions.TryAdd(type, subscription);
            }
            else
            {
                subscription = _subscribtions[type] as Subscription<T>;

                if (subscription == null)
                {
                    subscription = new Subscription<T>();
                    _subscribtions.TryAdd(type, subscription);
                }
            }

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