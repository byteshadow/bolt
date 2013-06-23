using System;
using System.Net.Sockets;

namespace BoltMQ.Core.Interfaces
{
    public interface ISession : IDisposable
    {
        event EventHandler<ISession> OnDisconnected;
        IStreamHandler StreamHandler { get; }
        Guid SessionId { get; }
        bool IsConnected { get; }
        Socket Socket { get; }
        SocketAsyncEventArgs ReceiveEventArgs { get; }
        SocketAsyncEventArgs SendEventArgs { get; }
        void SendAsync(byte[] data);
        //void ReceiveAsync(SocketAsyncEventArgs args);
        void Close();
        int Send(byte[] byteMessage);
        void FlushSendBuffer();
        void OnReceiveCompleted(Action<SocketAsyncEventArgs> onReceiveCompleted);
    }
}