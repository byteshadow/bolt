using System;
using System.Text;
using BoltMQ.Core;
using BoltMQ.Core.Collection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BoltMQ.Tests
{
    [TestClass]
    public class RingBufferTest
    {
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private readonly Random _rng = new Random();
        [TestMethod]
        public void GivenAnEmptyBufferFreeBytesShouldBeTheSizeOfTheBuffer()
        {
            //Arrange & Act
            BytesRingBuffer bytesRingBuffer = new BytesRingBuffer(10);

            //Assert
            Assert.AreEqual(10, bytesRingBuffer.FreeBytes);
            Assert.AreEqual(0, bytesRingBuffer.WriteOffset, "Writeoffset should be zero");
            Assert.AreEqual(0, bytesRingBuffer.ReadOffset, "ReadOffset should be zero");
        }

        [TestMethod]
        public void GivenAnHalfEmptyBufferFreeBytesShouldBeTheHalfTheSizeOfTheBuffer()
        {
            //arrange
            BytesRingBuffer bytesRingBuffer = new BytesRingBuffer(10);
            string temp = new string('x', 5);
            byte[] tempInBytes = Encoding.UTF8.GetBytes(temp);

            Assert.AreEqual(5, tempInBytes.Length);

            //act
            bytesRingBuffer.Write(tempInBytes, 0, tempInBytes.Length);

            //assert
            Assert.AreEqual(5, bytesRingBuffer.FreeBytes, "Free bytes should be 5");
            Assert.AreEqual(5, bytesRingBuffer.WriteOffset, "WriteOffset should be 5");

            Assert.AreEqual(0, bytesRingBuffer.ReadOffset, "ReadOffset should be zero");
        }

        [TestMethod]
        public void GivenAFullBufferFreeBytesShoulsbeZero()
        {
            //arrange
            BytesRingBuffer bytesRingBuffer = new BytesRingBuffer(10);
            string temp = new string('x', bytesRingBuffer.FreeBytes);
            byte[] tempInBytes = Encoding.UTF8.GetBytes(temp);

            Assert.AreEqual(bytesRingBuffer.FreeBytes, tempInBytes.Length, "Free bytes should equal the length");

            //act
            bytesRingBuffer.Write(tempInBytes, 0, tempInBytes.Length);

            //assert
            Assert.AreEqual(0, bytesRingBuffer.FreeBytes, "There should be no free bytes");
            Assert.AreEqual(bytesRingBuffer.Length, bytesRingBuffer.WriteOffset, "Writeoffset should be equal to length");
            Assert.AreEqual(0, bytesRingBuffer.ReadOffset, "ReadOffset should be zero");
        }

        [TestMethod]
        public void FillHalfTheBufferThenReadThenWriteTheRemainingSize()
        {
            //arrange
            BytesRingBuffer bytesRingBuffer = new BytesRingBuffer(10);
            string temp = new string('x', 5);
            byte[] tempInBytes = Encoding.UTF8.GetBytes(temp);

            //act
            bytesRingBuffer.Write(tempInBytes, 0, tempInBytes.Length);

            //Assert
            Assert.AreEqual(5, bytesRingBuffer.FreeBytes);

            //act
            byte[] readBuffer = new byte[10];
            int bytesCopied;
            bytesRingBuffer.Read(readBuffer, 0, readBuffer.Length, out bytesCopied);

            //Assert
            Assert.AreEqual(5, bytesRingBuffer.ReadOffset, "ReadOffset should have moved");
            Assert.AreEqual(10, bytesRingBuffer.FreeBytes, "Whole buffer should be free");

            tempInBytes = Encoding.UTF8.GetBytes(new string('x', 10));
            var wrote = bytesRingBuffer.Write(tempInBytes, 0, tempInBytes.Length);
            Assert.IsTrue(wrote, "Should have written the buffer.");

            var read = bytesRingBuffer.Read(readBuffer, 0, readBuffer.Length, out bytesCopied);
            Assert.IsTrue(read, "Should have read the buffer.");
            Assert.AreEqual(new string('x', 10), Encoding.UTF8.GetString(readBuffer), "Should be 10*x");
        }

        [TestMethod]
        public void RandomlyWriteAndReadFromBuffer()
        {
            //arrange
            int length = _rng.Next(1000, 100000);
            string message = RandomString(length);
            byte[] source = Encoding.UTF8.GetBytes(message);
            byte[] destination = new byte[length];

            int readOffset = 0, writeOffset = 0;
            BytesRingBuffer bytesRingBuffer = new BytesRingBuffer(256);

            //act
            while (writeOffset < source.Length)
            {
                int randomWrites = _rng.Next(1, Math.Min(bytesRingBuffer.FreeBytes, source.Length - writeOffset));
                bytesRingBuffer.Write(source, writeOffset, randomWrites);
                writeOffset += randomWrites;

                int tempReadoffset = 0;
                while (tempReadoffset < randomWrites)
                {
                    int remaining = randomWrites - tempReadoffset;

                    int randomRead = _rng.Next(1, remaining);

                    int bytesCopied;
                    bytesRingBuffer.Read(destination, readOffset, randomRead, out bytesCopied);
                    readOffset += randomRead;
                    tempReadoffset += randomRead;
                }
            }

            //assert
            Assert.AreEqual(message, Encoding.UTF8.GetString(destination));
        }

        private string RandomString(int size)
        {
            char[] buffer = new char[size];

            for (int i = 0; i < size; i++)
            {
                buffer[i] = Chars[_rng.Next(Chars.Length)];
            }
            return new string(buffer);
        }
    }
}
