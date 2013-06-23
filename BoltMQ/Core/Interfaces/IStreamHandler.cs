using System;
using System.Collections.Generic;

namespace BoltMQ.Core.Interfaces
{
    public interface IStreamHandler:IDisposable
    {
        Guid SessionId { get; }
        Exception StreamHandlerException { get; }
        bool ParseStream(byte[] buffer, int offset, int length);
    }
}