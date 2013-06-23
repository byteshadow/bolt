using System;
using System.Diagnostics;
using System.Threading;

namespace BoltMQ.Core.Collection
{
    /// <summary>
    /// Single Writer Single Reader RingBuffer
    /// </summary>
    /// <typeparam name="T">Type of item the buffer holds</typeparam>
    public class RingBuffer<T> : RingBufferBase<T>
    {
        private long _readCount = 0;
        private long _writeCount = 0;

        private long _writeClaimCursor = 0;
        private long _readClaimCursor = 0;

        AutoResetEvent _readResetEvent = new AutoResetEvent(false);

        public RingBuffer(int capacity, Func<int, IRingBufferItem<T>> factory)
            : base(capacity, factory) { }

        public int Count
        {
            get
            {
                var readCount = Interlocked.Read(ref _readCount);
                var writeCount = Interlocked.Read(ref _writeCount);
                return (int)(writeCount - readCount);
            }
        }

        public T Read(out int index)
        {
            var claimSlot = ClaimNextRead();

            if (claimSlot == null)
            {
                index = -1;
                return default(T);
            }

            T item = claimSlot.Item;
            index = claimSlot.Index;

            CommitRead(claimSlot);

            return item;
        }

        public int Write(T item)
        {
            var claim = ClaimNextWrite();

            if (claim == null)
                return -1;

            claim.Item = item;
            int index = claim.Index;

            CommitWrite(claim);
            return index;
        }

        public IRingBufferItem<T> ClaimNextWrite()
        {
            lock (_buffer)
            {


                long writeClaimCursor = Interlocked.Read(ref _writeClaimCursor);
                long readCount = Interlocked.Read(ref _readCount);

                int writeClaimIndex = (int)(writeClaimCursor % Capacity);
                int readIndex = (int)(readCount % Capacity);

                //Check the write Index should not overlap the read index
                if (writeClaimIndex == readIndex && writeClaimCursor > readCount)
                    return null;

                //Move the claim cursor
                Interlocked.Increment(ref _writeClaimCursor);

                return this[writeClaimIndex];
            }
        }

        public void CommitWrite(IRingBufferItem<T> item)
        {
            Interlocked.Increment(ref _writeCount);
        }

        public IRingBufferItem<T> ClaimNextRead()
        {
            lock (_buffer)
            {
                var readClaimCursor = Interlocked.Read(ref _readClaimCursor);
                var writeCount = Interlocked.Read(ref _writeCount);

                if (readClaimCursor == writeCount)
                    return default(IRingBufferItem<T>);

                int readClaimIndex = (int)(readClaimCursor % Capacity);

                //Move the claim cursor
                Interlocked.Increment(ref _readClaimCursor);

                return this[readClaimIndex];
            }
        }

        public void CommitRead(IRingBufferItem<T> item)
        {
            Interlocked.Increment(ref _readCount);

            _readResetEvent.Set();
        }

        public bool WriteWait(int millisecondsTimeout)
        {
            return _readResetEvent.WaitOne(millisecondsTimeout);
        }
    }
}