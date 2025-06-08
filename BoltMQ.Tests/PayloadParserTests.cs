using System;
using BoltMQ;
using BoltMQ.Core.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace BoltMQ.Tests
{
    [Message(3)]
    [ProtoContract]
    public class ParserMessage : IMessage
    {
        [ProtoMember(1)]
        public string Text { get; set; }
    }

    [TestClass]
    public class PayloadParserTests
    {
        [TestMethod]
        public void Write_InChunks_ShouldParseCompletePayload()
        {
            var serializer = new Serializer();
            byte[] payload = serializer.Serialize(new ParserMessage { Text = "hi" });
            PayloadParser parser = new PayloadParser();

            int offset = 0;
            bool complete = false;
            while (offset < payload.Length)
            {
                int chunk = Math.Min(2, payload.Length - offset);
                int bytesCopied;
                complete = parser.Write(payload, offset, chunk, out bytesCopied);
                offset += bytesCopied;
            }

            Assert.IsTrue(complete, "Parser should signal completion");
            CollectionAssert.AreEqual(payload, parser.Buffer);
        }
    }
}
