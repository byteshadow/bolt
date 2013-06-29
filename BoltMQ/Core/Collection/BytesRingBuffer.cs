using System;
using System.ServiceModel.Channels;
using System.Threading;

namespace BoltMQ.Core.Collection
{
    public sealed class BytesRingBuffer
    {
        private long _readCount;
        private long _writeCount;

        private readonly byte[] _buffer;
        private readonly int _bufferLength;
        private readonly BufferManager _bufferManager;
        private volatile int _pendingLength;
        private AutoResetEvent _freeSpaceHandler;

        public int ReadOffset
        {
            get { return (int)_readCount; }
        }

        public int WriteOffset
        {
            get { return (int)_writeCount; }
        }

        public int Length
        {
            get { return _bufferLength; }
        }

        public BytesRingBuffer(int capacity)
        {
            _buffer = new byte[capacity];
            _bufferLength = capacity;
            _bufferManager = BufferManager.CreateBufferManager(1, capacity);
        }

        public bool Write(byte[] data, int offset, int length)
        {
            //check we have enough to copy the whole data
            if (length > FreeBytes)
                return false;

            long writeCount = Interlocked.Read(ref  _writeCount);
            long readCount = Interlocked.Read(ref _readCount);

            int writeIndex = (int)(writeCount % _bufferLength);
            int readIndex = (int)(readCount % _bufferLength);

            int freeByteToEndOfBuffer = _bufferLength - writeIndex;

            if (writeIndex > readIndex || length > freeByteToEndOfBuffer)
            {
                int bytesToCopy = Math.Min(freeByteToEndOfBuffer, length);
                Buffer.BlockCopy(data, offset, _buffer, writeIndex, bytesToCopy);
                int bytesCopied = bytesToCopy;
                writeCount += bytesCopied;

                if (bytesCopied < length)
                {
                    writeIndex = (int)(writeCount % _bufferLength);
                    int remainingBytes = length - bytesCopied;
                    Buffer.BlockCopy(data, offset + bytesCopied, _buffer, writeIndex, remainingBytes);
                }
            }
            else
            {
                int freeBytes = readIndex == writeIndex ? _bufferLength : readIndex - writeIndex;
                int bytesToCopy = Math.Min(freeBytes, length);
                Buffer.BlockCopy(data, offset, _buffer, writeIndex, bytesToCopy);
            }

            //Move the write offset i.e. Commit
            Interlocked.Add(ref _writeCount, length);

            return true;
        }

        public bool Read(byte[] destination, int offset, int length, out int bytesRead)
        {
            bytesRead = 0;

            if (UnreadBytes == 0)
                return false;

            long writeCount = Interlocked.Read(ref  _writeCount);
            long readCount = Interlocked.Read(ref _readCount);

            int readIndex = (int) (readCount % _bufferLength);

            //Check if the write index is a head of the read
            if ((writeCount % _bufferLength) > (readCount % _bufferLength))
            {
                int unreadBytes = (int) (writeCount - readCount);
                int bytesToCopy = Math.Min(unreadBytes, length);
                Buffer.BlockCopy(_buffer, readIndex, destination, offset, bytesToCopy);
                bytesRead = bytesToCopy;

                Interlocked.Add(ref _readCount, bytesRead);
            }
            else
            {
                int unreadBytesToEndOfBuffer = _bufferLength - readIndex;
                int bytesToCopy = Math.Min(unreadBytesToEndOfBuffer, length);
                Buffer.BlockCopy(_buffer, readIndex, destination, offset, bytesToCopy);
                int bytesCopied = bytesToCopy;
                Interlocked.Add(ref _readCount, bytesCopied);

                bytesRead = bytesCopied;

                if (UnreadBytes > 0 && bytesCopied < length)
                {
                    readCount = Interlocked.Read(ref _readCount);
                    writeCount = Interlocked.Read(ref  _writeCount);

                    readIndex = (int) (readCount % _bufferLength);

                    int unreadBytesToWriteIndex = (int) (writeCount - readCount);

                    bytesToCopy = Math.Min(unreadBytesToWriteIndex, length - bytesCopied);
                    Buffer.BlockCopy(_buffer, readIndex, destination, offset + bytesCopied, bytesToCopy);

                    bytesCopied = bytesToCopy;
                    Interlocked.Add(ref _readCount, bytesCopied);
                    bytesRead += bytesCopied;
                }
            }

            if (_pendingLength > 0 && _freeSpaceHandler != null)
            {
                var handler = _freeSpaceHandler;

                _freeSpaceHandler = null;
                _pendingLength = 0;

                if (handler != null)
                    handler.Set();
            }
            return true;
        }

        public byte[] ReadAll(out int bytesRead)
        {
            int unreadBytes = UnreadBytes;

            if (unreadBytes == 0)
            {
                bytesRead = 0;
                return null;
            }

            byte[] data = _bufferManager.TakeBuffer(unreadBytes);

            Read(data, 0, unreadBytes, out bytesRead);

            return data;
        }

        public void Release(byte[] buffer)
        {
            _bufferManager.ReturnBuffer(buffer);
        }

        public int FreeBytes
        {
            get { return _bufferLength - UnreadBytes; }
        }

        public int UnreadBytes
        {
            get
            {
                long writeCount = Interlocked.Read(ref _writeCount);
                long readCount = Interlocked.Read(ref _readCount);

                return (int)(writeCount - readCount);
            }
        }

        public void NotifyFreeSpace(int length, AutoResetEvent freeSpaceHandler)
        {
            _pendingLength = length;
            _freeSpaceHandler = freeSpaceHandler;
        }
    }
}