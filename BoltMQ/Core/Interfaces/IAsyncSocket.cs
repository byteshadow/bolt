using System;
using System.Net.Sockets;

namespace BoltMQ.Core.Interfaces
{
    public interface IAsyncSocket : IDisposable
    {
        int ReceiveBufferSize { get; set; }

        AddressFamily AddressFamily { get; set; }
        SocketType SocketType { get; set; }
        ProtocolType ProtocolType { get; set; }
        Socket Socket { get; }

        void AddMessageHandler<T>(EventHandler<BoltEventArgs<T>> messageHandler);
        void RemoveMessageHandler<T>(EventHandler<BoltEventArgs<T>> messageHandler);

        void Close();
        void SendAsync<T>(T message);
        void ReceiveAsync(SocketAsyncEventArgs args);
    }
}