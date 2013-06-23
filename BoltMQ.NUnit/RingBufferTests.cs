using System;
using System.Threading;
using System.Threading.Tasks;
using BoltMQ.Core;
using BoltMQ.Core.Collection;
using NUnit.Framework;

namespace BoltMQ.NUnit
{
    [TestFixture]
    public class RingBufferTests
    {
        [Test]
        public void GivenANonePowerOfTwoCapacityTheNextPowerOfTwoIsUsed()
        {
            //Arrange
            const int capacity = 10;

            //Act
            RingBuffer<int> ringBuffer = new RingBuffer<int>(capacity, i => new RingBufferItem<int>(i));

            //Assert
            Assert.AreEqual(16, ringBuffer.Capacity);
        }

        [Test]
        public void GivenAPowerOfTwoCapacityGivenCapacityIsUsed()
        {
            //Arrange
            const int capacity = 16;

            //Act
            RingBuffer<int> ringBuffer = new RingBuffer<int>(capacity, i => new RingBufferItem<int>(i));

            //Assert
            Assert.AreEqual(16, ringBuffer.Capacity);
        }

        [Test]
        public void FillBufferNoMoreItemsAreAdded()
        {
            //Arrange
            const int capacity = 256;
            RingBuffer<object> ringBuffer = new RingBuffer<object>(capacity, i => new RingBufferItem<object>(i));
            Assert.AreEqual(256, ringBuffer.Capacity);

            //act
            for (int i = 0; i < capacity; i++)
            {
                var indexUsed = ringBuffer.Write(new object());
                Assert.AreEqual(i, indexUsed, "The write index should match the loop index");
            }

            //Assert
            var index = ringBuffer.Write(new object());

            Assert.AreEqual(-1, index);
        }

        [Test]
        public void FillBufferThenReadHalfThenWriteHalf()
        {
            //Arrange
            const int capacity = 256;
            RingBuffer<object> ringBuffer = new RingBuffer<object>(capacity, i => new RingBufferItem<object>(i));
            Assert.AreEqual(256, ringBuffer.Capacity);

            //act
            for (int i = 0; i < capacity; i++)
            {
                var indexUsed = ringBuffer.Write(new object());
                Assert.AreEqual(i, indexUsed, "The write index should match the loop index");
            }

            //The buffer should be full, so no more writes are allowed
            var index = ringBuffer.Write(new object());
            Assert.AreEqual(-1, index);

            for (int i = 0; i < capacity / 2; i++)
            {
                int readIndex;
                var item = ringBuffer.Read(out readIndex);
                Assert.IsNotNull(item, "Item should not be null");
                Assert.AreEqual(i, readIndex, "The Read index should be same as the loop index");
            }

            //Write again from the beginning
            for (int i = 0; i < capacity / 2; i++)
            {
                var indexUsed = ringBuffer.Write(new object());
                Assert.AreEqual(i, indexUsed, "The write index should match the loop index");
            }

            //Assert
            index = ringBuffer.Write(new object());
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void ReadEmptyBuffer()
        {
            //Arrange
            const int capacity = 64;
            RingBuffer<object> ringBuffer = new RingBuffer<object>(capacity, i => new RingBufferItem<object>(i));
            Assert.AreEqual(64, ringBuffer.Capacity);

            //Act
            int readIndex;
            object read = ringBuffer.Read(out readIndex);

            //Assert
            Assert.IsNull(read, "The read object should be null");
            Assert.AreEqual(-1, readIndex, "readIndex should not be positive");
        }

        [Test]
        public void FillHalfTheBufferThenReadAll()
        {
            //Arrange
            const int capacity = 256;
            RingBuffer<object> ringBuffer = new RingBuffer<object>(capacity, index => new RingBufferItem<object>(index));
            Assert.AreEqual(256, ringBuffer.Capacity);

            //act
            for (int j = 0; j < capacity / 2; j++)
            {
                var indexUsed = ringBuffer.Write(new object());
                Assert.AreEqual(j, indexUsed, "The write index should match the loop index");
            }

            int i;
            int readIndex = 0;
            for (i = 0; i < capacity; i++)
            {
                var item = ringBuffer.Read(out readIndex);
                if (readIndex == -1)
                    break;
                Assert.IsNotNull(item);
                Assert.AreEqual(i, readIndex, "The Read index should be same as the loop index");
            }

            //Assert
            Assert.AreEqual(-1, readIndex);
            Assert.AreEqual(capacity / 2, i);
        }

        [Test]
        public void ConcurrentReadWrite()
        {
            //Arrange
            const int capacity = 128;
            RingBuffer<object> ringBuffer = new RingBuffer<object>(capacity, i => new RingBufferItem<object>(i));

            AutoResetEvent resetEvent = new AutoResetEvent(false);
            Action writerAction = () =>
            {
                for (int i = 0; i < capacity; i++)
                {
                    int writeIndex = ringBuffer.Write(new object());
                    Assert.AreEqual(i, writeIndex);
                    resetEvent.Set();
                }

            };

            Action readerAction = () =>
                {

                    for (int i = 0; i < capacity; i++)
                    {
                        int readIndex;
                        object read = ringBuffer.Read(out readIndex);
                        if (readIndex < 0)
                        {
                            resetEvent.WaitOne(100);
                            read = ringBuffer.Read(out readIndex);
                        }
                        Assert.AreEqual(i, readIndex, "readIndex does not equal loop index.");
                        Assert.IsNotNull(read, "read object in null");
                    }
                };

            Parallel.Invoke(writerAction, readerAction);
        }
    }
}
