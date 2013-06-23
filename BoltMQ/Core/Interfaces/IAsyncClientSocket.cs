using System.Net;

namespace BoltMQ.Core.Interfaces
{
    public interface IAsyncClientSocket : IAsyncSocket
    {
        void Connect(IPEndPoint ipEndPoint);
        void Send<T>(T message);
        IPEndPoint RemoteIPEndPoint { get; }
        ISession Session { get; }
        bool Connected { get; set; }
    }
}