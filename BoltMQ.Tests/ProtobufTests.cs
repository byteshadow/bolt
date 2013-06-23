using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace BoltMQ.Tests
{
    [TestClass]
    public class ProtobufTests
    {
        [TestMethod]
        public void SerializeString()
        {
            var text = "Hello World!";
            byte[] buffer;

            using (MemoryStream stream = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(stream, text);
                buffer = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(buffer, 0, buffer.Length);
                stream.Position = 0;
                var typeId = ProtoBuf.ProtoReader.DirectReadVarintInt32(stream);
                
                //uint header = (((uint)fieldNumber) << 3) | (((uint)wireType) & 7);

                var normalBytes = Encoding.UTF8.GetBytes(text);
            }
        }

        [TestMethod]
        public void BitShiftingTest()
        {
            WireType wireType = WireType.String;
            int fieldNumber = 2;

            uint header = (((uint)fieldNumber) << 3) | (((uint)wireType) & 7);
            var a = header >> 3;
            var b = header & 7;
        }
    }
}
