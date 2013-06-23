using System;

namespace BoltMQ.Core.Exceptions
{
    public class NullBufferException : Exception
    {
        public NullBufferException()
            : base("Buffer is null.") { }
    }
}