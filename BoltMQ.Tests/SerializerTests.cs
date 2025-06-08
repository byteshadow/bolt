using System;
using ProtoBuf;
using BoltMQ;
using BoltMQ.Core.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BoltMQ.Tests
{
    [Message(2)]
    [ProtoContract]
    public class TestMessage : IMessage
    {
        [ProtoMember(1)]
        public int Number { get; set; }
    }

    [TestClass]
    public class SerializerTests
    {
        [TestMethod]
        public void SerializeAndDeserialize_ShouldRoundTrip()
        {
            var serializer = new Serializer();
            var msg = new TestMessage { Number = 42 };

            byte[] buffer = serializer.Serialize(msg);
            var result = (TestMessage)serializer.Deserialize(buffer);

            Assert.AreEqual(msg.Number, result.Number);
        }

        [TestMethod]
        public void ResolveTypeId_ShouldReturnRegisteredType()
        {
            var serializer = new Serializer();
            ushort id = serializer.ResolveType(typeof(TestMessage));
            Assert.AreEqual(typeof(TestMessage), serializer.ResolveType(id));
        }
    }
}
