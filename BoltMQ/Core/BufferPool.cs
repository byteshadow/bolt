using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using BoltMQ.Core.Interfaces;

namespace BoltMQ.Core
{
    public class BufferPool : IBufferPool
    {
        private byte[] _buffer;
        private Stack<int> _freePoolIndexes;

        public int TotalSize { get; private set; }
        public int Count { get; set; }
        public int SegmentSize { get; set; }

        public BufferPool(int count, int segmentSize)
        {
            if (count <= 0)
                throw new ArgumentException("Count cannot be less than or equal to ZERO.");

            if (segmentSize <= 0)
                throw new ArgumentException("SegementSize cannot be less than or equal to ZERO.");

            if (segmentSize % 2 != 0)
                throw new ArgumentException("SegmentSize must be of divisible of 2.");

            Count = count;
            SegmentSize = segmentSize;

            Init();

        }

        private void Init()
        {
            TotalSize = SegmentSize * Count;

            // create one large buffer and divide that  
            // out between each of SocketAsyncEventArg objects
            _buffer = new byte[TotalSize];

            _freePoolIndexes = new Stack<int>(Count);

            for (int i = 0; i < Count; i++)
            {
                _freePoolIndexes.Push(i * SegmentSize);
            }
        }

        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            lock (_freePoolIndexes)
            {
                if (_freePoolIndexes.Count == 0)
                {
                    return false;
                }

                args.SetBuffer(_buffer, _freePoolIndexes.Pop(), SegmentSize);
                return true;
            }
        }

        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            lock (_freePoolIndexes)
            {
                _freePoolIndexes.Push(args.Offset);
                args.SetBuffer(null, 0, 0);
            }
        }
    }
}