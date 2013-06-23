using System;
using BoltMQ.Core;

namespace BoltMQ
{
    internal interface ISubscribtion
    {
        Type SubscribtionType { get; }
        void BroadcastMessage(BoltEventArgs<object> args);
    }
}