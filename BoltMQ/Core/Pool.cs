using System;
using System.Collections.Generic;

namespace BoltMQ.Core
{
    // Represents a collection of reusable objects.   
    public class Pool<T>
    {
        readonly Stack<T> _pool;
        private readonly Func<T> _factory;

        // Initializes the object pool to the specified size 
        // 
        // The "capacity" parameter is the maximum number of 
        // objects the pool can hold 
        public Pool(int capacity, Func<T> factory)
        {
            _pool = new Stack<T>(capacity);
            _factory = factory;
        }

        // Add a object instance to the pool 
        // 
        //The "item" parameter is the object instance 
        // to add to the pool 
        public void Push(T item)
        {
            if (Equals(item, default(T)))
            {
                throw new ArgumentNullException("item", "Items added to a Pool cannot be null");
            }

            lock (_pool)
            {
                _pool.Push(item);
            }
        }

        // Removes a object instance from the pool 
        // and returns the object removed from the pool 
        public T Pop()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                    return _pool.Pop();

                return _factory != null ? _factory() : default(T);
            }
        }

        // The number of object instances in the pool 
        public int Count
        {
            get { return _pool.Count; }
        }

    }
}