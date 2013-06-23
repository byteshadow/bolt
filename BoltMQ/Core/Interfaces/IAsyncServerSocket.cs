using System;
using System.Net;

namespace BoltMQ.Core.Interfaces
{
    public interface  IAsyncServerSocket : IAsyncSocket
    {
        int Port { get; set; }
        int MaxConnections { get; set; }
        void Bind(IPEndPoint ipEndPoint);
        bool Send<T>(T message, Guid clientId);
        void SendAsync<T>(T message, Guid sessionId);
    }
}