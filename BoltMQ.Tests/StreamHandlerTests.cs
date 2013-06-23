//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using BoltMQ.Core;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using NUnit.Framework;
//using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

//namespace BoltMQ.Tests
//{
//    [TestClass]
//    [TestFixture]
//    public class StreamHandlerTests
//    {
//        private readonly Random _rng = new Random();
//        private int _counter;
//        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
//        private const string HelloWorld = "Hello World!";
//        //readonly AutoResetEvent _event = new AutoResetEvent(false);
//        // private volatile string _message;
//        private MessageProcessor _processor;
//        private MessageBroker _messageBroker;
//        private Serializer _serializer;
//        // private List<string> _messages = new List<string>();

//        [SetUp]
//        [TestInitialize]
//        public void SetUp()
//        {
//            _serializer = new Serializer();
//            _messageBroker = new MessageBroker();
//            _processor = new MessageProcessor(_messageBroker, _serializer);
//        }

//        [TestCleanup]
//        public void TestCleanup()
//        {
//            _messageBroker.Unsubscribe<string>(StringMessageHandler);
//            //_messages.Clear();
//            //_message = string.Empty;
//        }

//        [TestMethod]
//        [Test]
//        public void Serializer_PrepareMessage_TransportMessageShouldHaveTheLengthPrefixed()
//        {
//            //Arrange
//            string message = "Hello World!";
//            Serializer serializer = new Serializer();


//            //Act
//            byte[] transportMessage = serializer.Serialize(message);

//            //Assert
//            int dataTypeLength;
//            int dataLength;
//            string data;
//            var messageLength = ParseByteArray(transportMessage, out dataTypeLength, out dataLength, out data);

//            Assert.AreEqual(message, data);
//        }

//        private static int ParseByteArray(byte[] transportMessage, out int dataTypeLength, out int dataLength, out string data)
//        {
//            int offset = 0;
//            int messageLength = BitConverter.ToInt32(transportMessage, offset);
//            offset += 4;

//            dataTypeLength = BitConverter.ToInt32(transportMessage, offset);
//            offset += 4;
//            offset += dataTypeLength;

//            dataLength = BitConverter.ToInt32(transportMessage, offset);
//            offset += 4;

//            using (MemoryStream ms = new MemoryStream(transportMessage, offset, dataLength))
//            {
//                //data = ProtoBuf.Serializer.NonGeneric.Deserialize(ms);

//                data = ProtoBuf.Serializer.Deserialize<string>(ms);
//            }


//            return messageLength;
//        }

//        [TestMethod]
//        [Test]
//        public void PrepareMessageThenProcessShouldBeTheSame()
//        {
//            //Arrange

//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()),Guid.NewGuid());

//            string message = "Hello World!";

//            //Act
//            byte[] transportMessage = _serializer.Serialize(message);
//            bool success = false;

//            IEnumerable<int> messageIndexes = streamHandler.CopyStreamBuffer(transportMessage, 0, transportMessage.Length, out success);

//            //Assert
//            Assert.IsTrue(success);

//            Assert.IsTrue(messageIndexes.Any());
//        }



//        private void StringMessageHandler(object sender, BoltEventArgs<string> mqEventArgs)
//        {
//            //_message = msg;
//            //_event.Set();
//            Interlocked.Increment(ref _counter);
//            Assert.IsNotNull(mqEventArgs.SessionId);

//        }

//        [TestMethod]
//        [Test]
//        public void PrepareMessageThenProcessOneByteAtATime()
//        {
//            //Arrange
//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()), Guid.NewGuid());

//            //Act
//            byte[] transportMessage = _serializer.Serialize(HelloWorld);

//            bool success = false;
//            List<string> messages = new List<string>();

//            for (int i = 0; i < transportMessage.Length; i++)
//            {
//                var msgs = streamHandler.CopyStreamBuffer(transportMessage, i, 1, out success);

//                messages.AddRange(msgs.Select(bytes => (string)_serializer.Deserialize(bytes)));

//                Assert.IsTrue(success, "streamHandler.CopyStreamBuffer should be true");

//                if (i == 0)
//                {
//                    Assert.IsNotNull(streamHandler.CurrentPayloadParser.Size.Buffer, "Size Buffer should not be null.");
//                    Assert.AreNotEqual(0, streamHandler.CurrentPayloadParser.Size.Buffer[0], "First byte of the size buffer should be set");

//                }
//                else if (i == 3)
//                {
//                    Assert.AreEqual(transportMessage.Length - 4, streamHandler.CurrentPayloadParser.Buffer.Length, "PayloadParser buffer length doesnot match.");
//                }
//            }

//            //Assert
//            Assert.AreEqual(HelloWorld, messages.FirstOrDefault());
//        }

//        //private void StringMessagesHandler(object sender, string msg)
//        //{
//        //    lock (_event)
//        //    {
//        //        //_message = msg;
//        //        _counter++;
//        //        //_messages.Add(msg);
//        //        _event.Set();
//        //    }

//        //}

//        [TestMethod]
//        [Test]
//        public void PrepareMessageThenProcessTwoBytesAtATime()
//        {
//            //Arrange
//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()), Guid.NewGuid());

//            //Act
//            byte[] transportMessage = _serializer.Serialize(HelloWorld);

//            bool success = false;
//            List<string> messages = new List<string>();

//            for (int i = 0; i < transportMessage.Length; i += 2)
//            {
//                IEnumerable<byte[]> msgs =
//                    streamHandler.CopyStreamBuffer(transportMessage, i, (transportMessage.Length - i) > 1 ? 2 : 1, out success);

//                messages.AddRange(Deserialize(msgs));

//                Assert.IsTrue(success, "streamHandler.CopyStreamBuffer should be true");

//                if (i == 0)
//                {
//                    Assert.IsNotNull(streamHandler.CurrentPayloadParser.Size.Buffer, "Size Buffer should not be null.");
//                    Assert.AreNotEqual(0, streamHandler.CurrentPayloadParser.Size.Buffer[0], "First byte of the size buffer should be set");

//                }
//                else if (i == 2)
//                {
//                    Assert.AreEqual(transportMessage.Length - 4, streamHandler.CurrentPayloadParser.Buffer.Length, "PayloadParser buffer length doesnot match.");
//                }
//            }

//            //Assert
//            Assert.AreEqual(HelloWorld, messages.FirstOrDefault());
//        }

//        private List<string> Deserialize(IEnumerable<byte[]> msgs)
//        {
//            return new List<string>(msgs.Select(bytes => (string)_serializer.Deserialize(bytes)));

//        }

//        [TestMethod]
//        [Test]
//        public void PrepareZeroLengthMessageThenProcess()
//        {
//            //Arrange
//            List<string> messages = new List<string>();
//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()), Guid.NewGuid());

//            //Act
//            byte[] transportMessage = _serializer.Serialize(string.Empty);

//            //Assert
//            bool success;
//            var msgs = streamHandler.CopyStreamBuffer(transportMessage, 0, transportMessage.Length, out success);
//            messages.AddRange(Deserialize(msgs));

//            Assert.IsTrue(success, "should be successful");

//            Assert.AreEqual(1, messages.Count, "There should be no messages");
//        }

//        [TestMethod]
//        [Test]
//        public void PrepareTwoMessagesThenProcess_TwoMessagesShouldBeProcessed()
//        {
//            //Arrange
//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()), Guid.NewGuid());

//            string helloWorld = "Hello World!";
//            byte[] message1 = _serializer.Serialize(helloWorld);

//            string helloBoltmq = "Hello BoltMQ!";
//            byte[] message2 = _serializer.Serialize(helloBoltmq);

//            byte[] joinedMessages = new byte[message1.Length + message2.Length];
//            Buffer.BlockCopy(message1, 0, joinedMessages, 0, message1.Length);
//            Buffer.BlockCopy(message2, 0, joinedMessages, message1.Length, message2.Length);

//            //Act
//            bool success;

//            var msgs = streamHandler.CopyStreamBuffer(joinedMessages, 0, joinedMessages.Length, out success);
//            List<string> messages = new List<string>();
//            messages.AddRange(Deserialize(msgs));
//            Assert.IsTrue(success, "should be successful");

//            //Assert
//            Assert.IsTrue(messages.Count > 0, "messages should greater than Zero");
//            Assert.AreEqual(2, messages.Count, "There should be 2 messages.");

//            Assert.AreEqual(helloWorld, messages[0]);
//            Assert.AreEqual(helloBoltmq, messages[1]);
//        }

//        [TestMethod]
//        [Test]
//        [Explicit]
//        public void PrepareTwoMessagesThenProcessInRandomPartialBytes_Repeat1e6Times()
//        {
//            //Arrange
//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()), Guid.NewGuid());

//            string helloWorld = "Hello World!";
//            byte[] message1 = _serializer.Serialize(helloWorld);

//            string helloBoltmq = "Hello BoltMQ!";
//            byte[] message2 = _serializer.Serialize(helloBoltmq);

//            byte[] joinedMessages = new byte[message1.Length + message2.Length];
//            Buffer.BlockCopy(message1, 0, joinedMessages, 0, message1.Length);
//            Buffer.BlockCopy(message2, 0, joinedMessages, message1.Length, message2.Length);

//            //Act
//            int length = joinedMessages.Length;
//            int offset, copyLength;
//            Random random = new Random();
//            List<string> messages = new List<string>();

//            for (int i = 0; i < 1e6; i++)
//            {
//                offset = 0;
//                List<int> chunks = new List<int>();

//                while (offset < length)
//                {
//                    copyLength = random.Next(1, length - offset);

//                    chunks.Add(copyLength);

//                    bool success;

//                    var msgs = streamHandler.CopyStreamBuffer(joinedMessages, offset, copyLength, out success);

//                    messages.AddRange(Deserialize(msgs));

//                    if (!success)
//                        Console.WriteLine(streamHandler.StreamHandlerException);

//                    Assert.IsTrue(success, "streamHandler.CopyStreamBuffer should have passed");

//                    offset += copyLength;
//                }

//                if (2 * (i + 1) != messages.Count)
//                {
//                    chunks.ForEach(Console.WriteLine);
//                }

//                Assert.AreEqual(2 * (i + 1), messages.Count, "There should be " + 2 * (i + 1) + " messages.");

//                Assert.AreEqual(helloWorld, messages[2 * i]);
//                Assert.AreEqual(helloBoltmq, messages[2 * i + 1]);
//            }
//        }

//        [TestMethod]
//        public void ReplayRandomChuncks()
//        {

//            //Arrange
//            BlockingStreamHandler streamHandler = new BlockingStreamHandler(new MessageProcessor(new MessageBroker(), new Serializer()), Guid.NewGuid());

//            string helloWorld = "Hello World!";
//            byte[] message1 = _serializer.Serialize(helloWorld);

//            string helloBoltmq = "Hello BoltMQ!";
//            byte[] message2 = _serializer.Serialize(helloBoltmq);

//            byte[] joinedMessages = new byte[message1.Length + message2.Length];
//            Buffer.BlockCopy(message1, 0, joinedMessages, 0, message1.Length);
//            Buffer.BlockCopy(message2, 0, joinedMessages, message1.Length, message2.Length);

//            //Act
//            int length = joinedMessages.Length;
//            int offset = 0, copyLength;
//            List<string> messages = new List<string>();

//            List<int> chuncks = new List<int>
//            {
//                39,
//                4,
//                9,
//                10,
//                5,
//                2,
//                7,
//                1,
//                1,
//                1
//            };

//            foreach (int chunck in chuncks)
//            {

//                copyLength = chunck;

//                bool success;

//                var msgs = streamHandler.CopyStreamBuffer(joinedMessages, offset, copyLength, out success);

//                messages.AddRange(Deserialize(msgs));

//                if (!success)
//                    Console.WriteLine(streamHandler.StreamHandlerException);

//                Assert.IsTrue(success, "streamHandler.CopyStreamBuffer should have passed");

//                offset += copyLength;
//            }
//            Assert.AreEqual(2, messages.Count, "There should be " + 2 + " messages.");

//            Assert.AreEqual(helloWorld, messages[0]);
//            Assert.AreEqual(helloBoltmq, messages[1]);
//        }
//        //[TestMethod]
//        //[Test]
//        //public void PrepareRandomNumberOfMessagesThenProcessInRandomPartialBytes_Repeat1e4Times()
//        //{
//        //    //Arrange
//        //    BlockingStreamHandler streamHandler = new BlockingStreamHandler();
//        //    Random random = new Random();

//        //    int numberOfMessages = random.Next(10, 1000);

//        //    Console.WriteLine("Number of messages: {0}", numberOfMessages);

//        //    List<byte[]> messagesInBytes = new List<byte[]>(numberOfMessages);
//        //    List<string> messagesRaw = new List<string>(numberOfMessages);

//        //    int totalLength = 0;
//        //    int numOfEmptyMsgs = 0;
//        //    for (int i = 0; i < numberOfMessages; i++)
//        //    {
//        //        string message = RandomString(random.Next(0, 20));
//        //        messagesRaw.Add(message);
//        //        if (string.IsNullOrEmpty(message))
//        //        {
//        //            message = string.Empty;
//        //            Console.WriteLine("Empty Message at index {0}", i);
//        //            numOfEmptyMsgs++;
//        //        }

//        //        byte[] inBytes = streamHandler.Prepare(Encoding.UTF8.GetBytes(message));
//        //        messagesInBytes.Add(inBytes);
//        //        totalLength += inBytes.Length;
//        //    }

//        //    byte[] joinedMessages = new byte[totalLength];
//        //    int offset = 0, copyLength = 0;

//        //    for (int i = 0; i < numberOfMessages; i++)
//        //    {
//        //        Buffer.BlockCopy(messagesInBytes[i], 0, joinedMessages, offset, messagesInBytes[i].Length);
//        //        offset += messagesInBytes[i].Length;
//        //    }

//        //    //Act
//        //    List<byte[]> messages = new List<byte[]>();
//        //    int length = joinedMessages.Length;

//        //    for (int i = 0; i < 1e4; i++)
//        //    {
//        //        offset = 0;
//        //        messages.Clear();

//        //        while (offset < length)
//        //        {
//        //            copyLength = random.Next(1, Math.Max(1, (length - offset) / 10));
//        //            if (i == 0)
//        //                Console.WriteLine(copyLength);

//        //            List<byte[]> decoded = streamHandler.(joinedMessages, offset, copyLength);
//        //            decoded.ForEach(messages.Add);
//        //            offset += copyLength;
//        //        }

//        //        Assert.AreEqual(numberOfMessages - numOfEmptyMsgs, messages.Count, "There should be " + numberOfMessages + " messages.");
//        //        Assert.AreEqual(StreamState.Idle, streamHandler.State);

//        //        int decrement = 0;
//        //        for (int j = 0; j < numberOfMessages; j++)
//        //        {
//        //            if (string.IsNullOrEmpty(messagesRaw[j]))
//        //            {
//        //                decrement++;
//        //                continue;
//        //            }

//        //            Assert.AreEqual(messagesRaw[j], Encoding.UTF8.GetString(messages[j - decrement]));
//        //        }
//        //    }
//        //}


//        private string RandomString(int size)
//        {
//            char[] buffer = new char[size];

//            for (int i = 0; i < size; i++)
//            {
//                buffer[i] = Chars[_rng.Next(Chars.Length)];
//            }
//            return new string(buffer);
//        }
//    }
//}
