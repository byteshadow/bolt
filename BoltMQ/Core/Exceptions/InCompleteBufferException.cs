using System;

namespace BoltMQ.Core.Exceptions
{
    public class InCompleteBufferException : Exception
    {
        public InCompleteBufferException(int length, int offset)
            : base(string.Format("The buffer is incomplete! Expected length \"{0}\", current offset \"{1}\"", length, offset))
        {
        }
    }
}