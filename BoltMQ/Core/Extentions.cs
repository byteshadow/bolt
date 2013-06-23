using System;
using System.Net.Sockets;
using System.Reactive.Linq;
namespace BoltMQ.Core
{
    public static class Extentions
    {
        public static IObservable<SocketAsyncEventArgs> ToObservable(this SocketAsyncEventArgs args)
        {
            return Observable.FromEventPattern<SocketAsyncEventArgs>(args, "Completed").Select(pattern => pattern.EventArgs);
        }
    }
}
