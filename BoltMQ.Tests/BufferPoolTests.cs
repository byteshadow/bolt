using System;
using System.Net.Sockets;
using BoltMQ.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BoltMQ.Tests
{
    [TestClass]
    public class BufferPoolTests
    {
        [TestMethod]
        public void SetBuffer_ShouldAssignBufferWhenAvailable()
        {
            BufferPool pool = new BufferPool(1, 8);
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();

            bool result = pool.SetBuffer(args);

            Assert.IsTrue(result, "Buffer should be assigned successfully");
            Assert.AreEqual(8, args.Count);
            Assert.IsNotNull(args.Buffer);
        }

        [TestMethod]
        public void FreeBuffer_ShouldReturnBufferForReuse()
        {
            BufferPool pool = new BufferPool(1, 8);
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            pool.SetBuffer(args);
            pool.FreeBuffer(args);

            SocketAsyncEventArgs args2 = new SocketAsyncEventArgs();
            bool result = pool.SetBuffer(args2);

            Assert.IsTrue(result, "Buffer from pool should be reusable");
        }
    }
}
