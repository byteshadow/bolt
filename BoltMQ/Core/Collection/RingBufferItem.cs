using System;
using System.Threading;

namespace BoltMQ.Core.Collection
{
    public enum RingBufferItemState
    {
        Readable = 0,
        Reading,
        Writable,
        Writting
    }

    public interface IRingBufferItem<T>
    {
        int Index { get; }
        T Item { get; set; }
    }

    public class RingBufferItem<T> : IRingBufferItem<T>
    {
        private readonly int _index;
        private readonly AutoResetEvent _readResetEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _writeResetEvent = new AutoResetEvent(false);
        private long _state = (long)RingBufferItemState.Writable;
        private T _item;

        public int Index
        {
            get { return _index; }
        }

        public RingBufferItemState State
        {
            get
            {
                var state = Interlocked.Read(ref _state);
                Thread.MemoryBarrier();
                return (RingBufferItemState)state;
            }
        }
        public T Item
        {
            get
            {
                if (!CanRead())
                {
                    throw new Exception("State out of order");
                }
                else
                {
                    var item = _item;
                    CommitRead();
                    return item;
                }
            }
            set
            {
                if (!CanWrite())
                {
                    throw new Exception("State out of order");
                }

                _item = value;
                CommitWrite();
            }
        }

        public AutoResetEvent ReadEvent
        {
            get { return _readResetEvent; }
        }

        public AutoResetEvent WriteEvent
        {
            get { return _writeResetEvent; }
        }

        public RingBufferItem(int index)
        {
            _index = index;
        }

        private bool CanRead()
        {
            var result = Interlocked.CompareExchange(ref _state,
                                                     (int)RingBufferItemState.Reading,
                                                     (int)RingBufferItemState.Readable);

            return result == (int)RingBufferItemState.Readable;
        }

        private void CommitRead()
        {
            Interlocked.Exchange(ref _state, (int)RingBufferItemState.Writable);
        }

        private void CommitWrite()
        {
            Interlocked.Exchange(ref _state, (int)RingBufferItemState.Readable);
        }

        private bool CanWrite()
        {
            var result = Interlocked.CompareExchange(ref _state,
                                                     (int)RingBufferItemState.Writting,
                                                     (int)RingBufferItemState.Writable);

            return result == (int)RingBufferItemState.Writable;
        }
    }
}