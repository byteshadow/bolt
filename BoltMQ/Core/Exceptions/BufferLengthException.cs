using System;

namespace BoltMQ.Core.Exceptions
{
    public class BufferLengthException : Exception
    {
        public BufferLengthException(int bufferSize)
            : base(string.Format("Buffer length is size {0} bytes", bufferSize)) { }
    }
}