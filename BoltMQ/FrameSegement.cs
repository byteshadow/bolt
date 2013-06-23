using System;
using System.Text;
using BoltMQ.Core.Exceptions;

namespace BoltMQ
{
    public class FrameSegement
    {
        public byte[] Buffer;
        public int Length;
        public int Offset;
        public int WriteOffset;

        public bool IsComplete { get { return Length == (WriteOffset - Offset); } }

        public int ReadAsInt()
        {
            if (Buffer == null)
                throw new NullBufferException();

            if (Length == 0)
                throw new BufferLengthException(0);
            
            if (!IsComplete)
                throw new InCompleteBufferException(Length, WriteOffset - Offset);

            return BitConverter.ToInt32(Buffer, Offset);
        }

        public string ReadAsUTF8String()
        {
            if (!IsComplete)
                throw new InCompleteBufferException(Length, WriteOffset - Offset);

            return Encoding.UTF8.GetString(Buffer, Offset, Length);
        }
    }
}