using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using BoltMQ.Core.Interfaces;

namespace BoltMQ.Core
{
    public abstract class AsyncServerSocket : AsyncSocket, IAsyncServerSocket
    {
        private bool _initialized;
        private ConcurrentDictionary<Guid, ISession> _activeConnections;
        private Semaphore _maxNumberAcceptedClients;
        private int _numConnectedSockets;

        /// <summary>
        /// The max number of connections the socket will accept
        /// </summary>
        public int MaxConnections { get; set; }

        public int ActiveConnectionsCount { get { return _numConnectedSockets; } }

        protected ConcurrentDictionary<Guid, ISession> ActiveConnections { get { return _activeConnections; } }

        /// <summary>
        /// Initializes the socket with the parameters set
        /// </summary>
        /// <param name="messageProcessor">Handles message distribution and deserialization</param>
        protected override void Initialize(IMessageProcessor messageProcessor)
        {
            if (_initialized)
                throw new NotSupportedException("Async Socket Server has already been initialized");

            base.Initialize(messageProcessor);

            _initialized = true;
        }

        /// <summary>
        /// The <see cref="IPEndPoint"/> the socket will bind to.
        /// </summary>
        /// <param name="ipEndPoint"></param>
        public void Bind(IPEndPoint ipEndPoint)
        {
            // create the socket which listens for incoming connections
            Socket = new Socket(ipEndPoint.AddressFamily, SocketType, ProtocolType);

            if (ipEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // based on http://blogs.msdn.com/wndp/archive/2006/10/24/creating-ip-agnostic-applications-part-2-dual-mode-sockets.aspx
                Socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
                ipEndPoint = new IPEndPoint(IPAddress.IPv6Any, ipEndPoint.Port);
            }

            SetupBufferPools(MaxConnections);

            //The Semaphore controls how many clients can connect at a time, this will be the number defined by the MaxConnection Property
            _maxNumberAcceptedClients = new Semaphore(MaxConnections, MaxConnections);

            _activeConnections = new ConcurrentDictionary<Guid, ISession>();

            // Associate the socket with the local endpoint.
            Socket.Bind(ipEndPoint);

            // Bind the server.
            Socket.Listen(MaxConnections);

            // Send initial accept on the listening socket.
            AcceptAsync(null);
        }

        private static int GetNetworkInterfaceMtu(IPEndPoint ipEndPoint)
        {
            IEnumerable<NetworkInterface> networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterfaceComponent ipType = ipEndPoint.AddressFamily == AddressFamily.InterNetworkV6
                                                   ? NetworkInterfaceComponent.IPv6
                                                   : NetworkInterfaceComponent.IPv4;

            foreach (NetworkInterface adapter in networkInterfaces)
            {
                if (!adapter.Supports(ipType)) continue;

                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    IPInterfaceProperties adapterProperties = adapter.GetIPProperties();

                    foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == ipEndPoint.AddressFamily)
                        {
                            if (Equals(ip.Address, ipEndPoint.Address))
                            {
                                switch (ipType)
                                {
                                    case NetworkInterfaceComponent.IPv4:
                                        var ipV4Props = adapterProperties.GetIPv4Properties();
                                        return ipV4Props.Mtu;
                                        break;
                                    case NetworkInterfaceComponent.IPv6:
                                        var ipV6Props = adapterProperties.GetIPv6Properties();
                                        return ipV6Props.Mtu;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }
                }
            }

            return DefaultBufferSize;
        }

        /// <summary>
        /// Recycles the Session back into the pool. This is the event handler for the <see cref="ISession"/> OnDisconnected event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="session"></param>
        protected void SessionClosed(object sender, ISession session)
        {
            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref _numConnectedSockets);

            // Release the semaphore
            _maxNumberAcceptedClients.Release();

            //Return the buffer back to the pool
            _receiveBufferPool.FreeBuffer(session.ReceiveEventArgs);
        }

        private void AcceptAsync(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += OnCompleted;
            }

            // Socket must be cleared since the context object is being reused.
            e.AcceptSocket = null;

            //Dont Accept new clients unless the number of clients is less than configured
            _maxNumberAcceptedClients.WaitOne();

            try
            {
                bool willRaiseEvent = Socket.AcceptAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessAccept(e);
                }
            }
            catch (ObjectDisposedException)
            {
                //If the socket closes while there is a pending accept it throws
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs asyncEvent)
        {
            Debug.Assert(asyncEvent != null, "ayncEvent != null");

            if (asyncEvent.AcceptSocket.Connected)
            {
                Interlocked.Increment(ref _numConnectedSockets);

                var connection = ConnectionFactory(asyncEvent.AcceptSocket);
                // assign a byte buffer from the buffer pool to the SocketAsyncEventArg objects
                _receiveBufferPool.SetBuffer(connection.ReceiveEventArgs);

                _activeConnections.TryAdd(connection.SessionId, connection);

                connection.ReceiveAsync(connection.ReceiveEventArgs);
            }

            // Accept the next Session request
            AcceptAsync(asyncEvent);
        }

        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Accept)
            {
                ProcessAccept(e);
            }
        }

        public bool Send<T>(T message, Guid sessionId)
        {
            if (_activeConnections.ContainsKey(sessionId))
            {
                MessageProcessor.Send(message, _activeConnections[sessionId]);
                return true;
            }

            return false;
        }

       

        /// <summary>
        /// Closes the socket
        /// </summary>
        public override void Close()
        {
            //close all the connections
            foreach (var activeConnection in _activeConnections)
            {
                activeConnection.Value.Close(activeConnection.Value);
            }
            Socket.Close();
        }

        public void Bind(string uriString)
        {
            Uri uri = new Uri(uriString);

            IPEndPoint ipEndPoint = GetIPEndPoint(uri);

            Bind(ipEndPoint);
        }

        public void Bind(int portNumber)
        {
            // Get host related information.
            IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            // Get endpoint for the listener.
            IPEndPoint localEndPoint = new IPEndPoint(addressList[addressList.Length - 1], portNumber);

            Bind(localEndPoint);
        }
    }
}