using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NUnit.Framework;
using ProtoBuf;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Serializer = BoltMQ.Serializer;

namespace BoltMQ.Tests
{
    [Message(1)]
    [ProtoContract]
    public class SampleMessage : IMessage
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public int Y { get; set; }
        [ProtoMember(3)]
        public int Z { get; set; }
        [ProtoMember(4)]
        public string FirstName { get; set; }
    }

    [TestFixture]
    [TestClass]
    public class BoltSocketTests
    {
        [TestMethod]
        [Test]
        public void AyncTcpServer()
        {
            ServerSocket serverSocket = new ServerSocket();
            serverSocket.Bind(9900);

            serverSocket.AddMessageHandler<SampleMessage>(SampleMessageHandler);

            // Get host related information.
            IPAddress[] addressList = Dns.GetHostEntry(Environment.MachineName).AddressList;

            // Get endpoint for the listener.
            IPEndPoint localEndPoint = new IPEndPoint(addressList[addressList.Length - 1], 9900);

            long totalTime = 0;
            const int msgs = (int)1e5;
            int msgLength = 0;
            Action action = () =>
                {
                    ClientSocket clientSocket = new ClientSocket();
                    clientSocket.Connect(localEndPoint);

                    Assert.IsTrue(clientSocket.Connected);

                    Stopwatch sw = Stopwatch.StartNew();
                    Serializer serializer = new Serializer();

                    var sample = new SampleMessage { X = 38 };
                    var msg = serializer.Serialize(sample);
                    msgLength = msg.Length;

                    for (int i = 0; i < msgs; i++)
                    {
                        clientSocket.Send(sample);
                    }

                    sw.Stop();

                    Interlocked.Add(ref totalTime, sw.ElapsedMilliseconds);

                    SpinWait.SpinUntil(() => counter == msgs, 2000);

                    //networkStream.Close();
                    clientSocket.Close();
                };

            List<Action> actions = new List<Action>();
            int numOfClients = 1;

            for (int i = 0; i < numOfClients; i++)
            {
                actions.Add(action);
            }

            Stopwatch sw2 = Stopwatch.StartNew();
            Parallel.Invoke(actions.ToArray());

            if (!Debugger.IsAttached)
                SpinWait.SpinUntil(() => counter == msgs * numOfClients, 2000);
            else
            {
                SpinWait.SpinUntil(() => counter == msgs * numOfClients, 60000);
            }

            sw2.Stop();


            Console.WriteLine("Num Of Msgs: {0:###,###}", counter);
            Console.WriteLine("Average for each client {0}ms", totalTime / actions.Count);
            Console.WriteLine("Average Speed for each client: {0:###,###}msgs/s", (msgs / (totalTime / actions.Count)) * 1000);
            Console.WriteLine("Total time: {0}ms", sw2.ElapsedMilliseconds);
            Console.WriteLine("Msgs/s {0:###,###}", (counter / sw2.ElapsedMilliseconds) * 1000);
            Console.WriteLine("Msg length {0}bytes", msgLength);
            Assert.AreEqual(msgs * numOfClients, counter, "Not all msgs received");
        }

        private void SampleMessageHandler(object sender, BoltEventArgs<SampleMessage> e)
        {
            Interlocked.Increment(ref counter);
        }

        AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private int counter = 0;
    }
}
