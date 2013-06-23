using System;

namespace BoltMQ.Core.Collection
{
    public abstract class RingBufferBase<T>
    {
        protected readonly IRingBufferItem<T>[] _buffer;

        public int Capacity { get; private set; }

        protected RingBufferBase(int capacity, Func<int, IRingBufferItem<T>> factory)
        {
            if (!IsPowerOfTwo((ulong)capacity))
                capacity = NextPowerOfTwo(capacity);

            Capacity = capacity;

            _buffer = new IRingBufferItem<T>[capacity];

            for (int i = 0; i < capacity; i++)
            {
                _buffer[i] = factory(i);
            }
        }

        protected IRingBufferItem<T> this[int index]
        {
            get { return _buffer[index]; }
        }

        public static bool IsPowerOfTwo(ulong x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        public static int NextPowerOfTwo(int n)
        {
            double y = Math.Floor(Math.Log(n, 2));
            return (int)Math.Pow(2, y + 1);
        }
    }
}