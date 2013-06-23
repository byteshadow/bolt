using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoltMQ;
using BoltMQ.Core;
using Shared;

namespace Client
{
    public class ConsoleClient
    {
        private static int _receivedMsgsCounter;
        private static int _messageCounter;
        private static int _maxMessages;
        private static CancellationTokenSource _cancellationToken;
        private static ClientSocket _clientSocket;
        private static AutoResetEvent _messageReceivedEvent = new AutoResetEvent(false);
        private const bool UseBolt = true;
        private static readonly Random _rng = new Random();
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static void Main(params string[] args)
        {
            using (var file = File.Create(string.Format("client-{0:yyyy_MM_dd-hh_mm_ss}.log", DateTime.Now)))
            {
                Trace.AutoFlush = true;
                Trace.Listeners.Add(new TextWriterTraceListener(file));

                Task.Factory.StartNew(ShowStats);

                if (UseBolt)
                    BoltClientSocket();
                else
                    TcpClient();
            }
        }

        private static void TcpClient()
        {
            System.Net.Sockets.TcpClient tcpClient = new TcpClient("127.0.0.1", 9900);

            var stream = tcpClient.GetStream();
            RequestMessage msg = new RequestMessage();
            msg.Integer = 5;
            Serializer serializer = new Serializer();
            var bytes = serializer.Serialize(msg);

            //for (int i = 0; i < 1000; i++)
            while (true)
            {
                stream.Write(bytes, 0, bytes.Length);
                Interlocked.Increment(ref _messageCounter);
            }
        }

        private static void BoltClientSocket()
        {
            _clientSocket = new ClientSocket();
            _clientSocket.Connect("tcp://127.0.0.1:9900");
            _clientSocket.MessageProcessor.MessageBroker.Subscribe<RequestMessage>(MessageHandler);
            _cancellationToken = new CancellationTokenSource();

            Task.Factory.StartNew(SendMessages, _cancellationToken.Token);

            Console.ReadLine();
            _clientSocket.Close();
        }

        private static void MessageHandler(object sender, BoltEventArgs<RequestMessage> boltEventArgs)
        {
            Interlocked.Increment(ref _receivedMsgsCounter);
            _messageReceivedEvent.Set();
        }

        private static void SendMessages()
        {
            Random random = new Random();
            RequestMessage msg = new RequestMessage();
            //msg.String = RandomString(200);
            int msgId = 0;
            while (!_cancellationToken.IsCancellationRequested)
            {
                //for (int i = 0; i < 1e4; i++)
                {
                    msg.Integer = msgId++;
                    
                    // msg.String = RandomString(256);
                    if (!_clientSocket.Connected)
                    {
                        //_cancellationToken.Cancel();
                        //break;
                        continue;

                    }
                    _clientSocket.SendAsync(msg);
                    Interlocked.Increment(ref _messageCounter);
                    //_messageReceivedEvent.WaitOne(1000);
                }
                //Thread.Sleep(random.Next(10, 1000));
            }
        }

        private static void ShowStats()
        {
            while (true)
            {
                Thread.Sleep(1000);

                var msgsCount = Interlocked.Exchange(ref _messageCounter, 0);
                _maxMessages = Math.Max(msgsCount, _maxMessages);
                var inputMsgs = Interlocked.Exchange(ref _receivedMsgsCounter, 0);
                Console.Clear();

                Console.WriteLine("Received: {0:###,###} msgs/s", inputMsgs);
                //Console.WriteLine("Connected: {0}", _clientSocket.Connected);
                Console.WriteLine("Speed: {0:###,###} msgs/s", msgsCount);
                Console.WriteLine("Max Speed: {0:###,###} msgs/s", _maxMessages);
            }
        }

        private static string RandomString(int size)
        {
            char[] buffer = new char[size];

            for (int i = 0; i < size; i++)
            {
                buffer[i] = Chars[_rng.Next(Chars.Length)];
            }
            return new string(buffer);
        }
    }
}
