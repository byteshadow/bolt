using System;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BoltMQ.Tests
{
    [Message(1)]
    public class BrokerMessage : IMessage
    {
        public string Value { get; set; }
    }

    [TestClass]
    public class MessageBrokerTests
    {
        [TestMethod]
        public void Broadcast_WithSubscriber_ShouldInvokeHandler()
        {
            MessageBroker broker = new MessageBroker();
            int callCount = 0;

            broker.Subscribe<BrokerMessage>((s, e) =>
            {
                callCount++;
                Assert.AreEqual("hello", e.Message.Value);
            });

            broker.Broadcast(new BoltEventArgs<object>(Guid.NewGuid(), new BrokerMessage { Value = "hello" }));

            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public void Broadcast_AfterUnsubscribe_ShouldNotInvokeHandler()
        {
            MessageBroker broker = new MessageBroker();
            int callCount = 0;
            EventHandler<BoltEventArgs<BrokerMessage>> handler = (s, e) => callCount++;
            broker.Subscribe(handler);

            broker.Unsubscribe(handler);
            broker.Broadcast(new BoltEventArgs<object>(Guid.NewGuid(), new BrokerMessage()));

            Assert.AreEqual(0, callCount);
        }
    }
}
