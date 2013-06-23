using System;
using BoltMQ.Core;

namespace BoltMQ
{
    internal class Subscription<T> : ISubscribtion
    {
        public event EventHandler<BoltEventArgs<T>> MessageReceived;

        private readonly Type _messageType;

        public Subscription()
        {
            _messageType = typeof(T);
        }

        public void OnMessageReceived(BoltEventArgs<T> e)
        {
            EventHandler<BoltEventArgs<T>> handler = MessageReceived;
            if (handler != null) 
                handler(this, e);
        }

        public Type SubscribtionType
        {
            get { return _messageType; }
        }

        public void BroadcastMessage(BoltEventArgs<object> args)
        {
            if (args != null)
                if (args.Message is T || _messageType.IsInstanceOfType(args.Message))
                {
                    OnMessageReceived(new BoltEventArgs<T>(args.SessionId, (T)args.Message));
                }
        }
    }
}