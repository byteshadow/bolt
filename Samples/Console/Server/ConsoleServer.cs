using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoltMQ;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;
using Shared;

namespace Server
{
    public class ConsoleServer
    {
        private static int _messageCounter;
        private static int _maxMessages;
        private static CancellationTokenSource _cancellationToken;
        private static ServerSocket _serverSocket;

        public static void Main(params string[] args)
        {
            using (var file = File.Create(string.Format("server-{0:yyyy_MM_dd-hh_mm_ss}.log", DateTime.Now)))
            {
                Trace.AutoFlush = true;
                Trace.Listeners.Add(new TextWriterTraceListener(file));
               
                _cancellationToken = new CancellationTokenSource();
                Task.Factory.StartNew(ShowStats, _cancellationToken.Token, TaskCreationOptions.LongRunning,
                                      TaskScheduler.Current);
                BoltSocket();
                //TcpServer();
                _cancellationToken.Cancel();
            }
        }

        private static void BoltSocket()
        {
            _serverSocket = new ServerSocket();
            _serverSocket.AddMessageHandler<RequestMessage>(RequestHandler);
            _serverSocket.Bind("tcp://127.0.0.1:9900");
            Console.ReadLine();

            _serverSocket.Close();
        }

        private static void TcpServer()
        {
            TcpListener listener = new TcpListener(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9900));
            listener.Start(1);
            var tcpClient = listener.AcceptTcpClient();
            using (var stream = tcpClient.GetStream())
            {
                byte[] buffer = new byte[2048];
                Serializer serializer = new Serializer();
                MessageBroker messageBroker = new MessageBroker();
                messageBroker.Subscribe<RequestMessage>(RequestHandler);
                MessageProcessor processor = new MessageProcessor(messageBroker, serializer);
                StreamHandler handler = new StreamHandler(processor, Guid.NewGuid());
                var read = stream.Read(buffer, 0, 2048);
                while (read > 0)
                {
                    handler.ParseStream(buffer, 0, read);
                    read = stream.Read(buffer, 0, 2048);
                }
            }
        }

        private static void ShowStats()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(333);

                var msgsCount = Interlocked.Exchange(ref _messageCounter, 0);
                _maxMessages = Math.Max(msgsCount, _maxMessages);

                Console.Clear();
                if (_serverSocket != null)
                {
                    Console.WriteLine("# Connections: {0}", _serverSocket.ActiveConnectionsCount);
                }
                Console.WriteLine("Speed: {0:###,###} msgs/s", msgsCount * 3);
                Console.WriteLine("Max Speed: {0:###,###} msgs/s", _maxMessages * 3);
            }
        }

        private static int previousMsgId = -1;
        private static void RequestHandler(object sender, BoltEventArgs<RequestMessage> e)
        {
            //if (previousMsgId == -1)
            //    previousMsgId = e.Message.Integer;
            //else
            //{
            //    if (previousMsgId + 1 != e.Message.Integer)
            //    {
            //        throw new InvalidOperationException("Missed a message.");
            //    }else
            //    {
            //        previousMsgId = e.Message.Integer;
            //    }
            //}
            Interlocked.Increment(ref _messageCounter);
            //_serverSocket.SendAsync(e.Message, e.SessionId);
        }
    }
}
