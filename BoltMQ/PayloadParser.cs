using System;
using System.IO;
using BoltMQ.Core.Exceptions;

namespace BoltMQ
{
    public class PayloadParser
    {
        private int _offset;
        private int _payloadlength;
        private byte[] _buffer;
        private const int LengthSize = sizeof(int);
        private readonly byte[] _lengthBuffer = new byte[LengthSize];
        private int _lengthBufferOffset;

        public PayloadParser()
        {
            Size = new FrameSegement();
            Size.Buffer = _lengthBuffer;

            BodyDataType = new Frame();
            Body = new Frame();
        }

        public PayloadParser(byte[] buffer)
        {
            if (buffer == null)
                throw new NullBufferException();

            SetBuffer(buffer);
        }

        public void SetBuffer(byte[] buffer)
        {
            /* 8 is the minimum total size length of the datatype & data */
            if (buffer.Length < 8)
                throw new ArgumentException("buffer length must be greater than 8.");

            _buffer = buffer;
            _payloadlength = buffer.Length;

            int dataTypeLength = BitConverter.ToInt32(buffer, 0);
            BodyDataType = new Frame(buffer, 0, 4 + dataTypeLength);

            int dataOffset = 4 + dataTypeLength;
            int dataLength = _payloadlength - dataOffset;
            Body = new Frame(buffer, dataOffset, dataLength);
        }

        public FrameSegement Size { get; set; }

        public Frame BodyDataType { get; set; }

        public Frame Body { get; set; }

        public bool IsComplete
        {
            get { return _payloadlength == _offset; }
        }

        public byte[] Buffer
        {
            get { return _buffer; }
        }

        public bool Write(byte[] buffer, int offset, int length, out int bytesCopied)
        {
            bytesCopied = 0;
            bool isComplete = false;
            int localOffset = offset;

            //do we know the length of the payload yet?
            if (_payloadlength == 0)
            {
                //do we have a partial length buffer?
                if (_lengthBufferOffset > 0 && _lengthBufferOffset < LengthSize)
                {
                    int remainingLengthBytes = LengthSize - _lengthBufferOffset;
                    int bytesToCopy = remainingLengthBytes < length ? remainingLengthBytes : length;
                    System.Buffer.BlockCopy(buffer, localOffset, _lengthBuffer, _lengthBufferOffset, bytesToCopy);
                    bytesCopied += bytesToCopy;
                    _lengthBufferOffset += bytesToCopy;
                    localOffset += bytesToCopy;

                    if (_lengthBufferOffset == LengthSize)
                    {
                        _payloadlength = BitConverter.ToInt32(_lengthBuffer, 0);
                        _buffer = new byte[_payloadlength];
                    }
                    else
                    {
                        return isComplete = false;
                    }
                }
                else
                {
                    //check if we can read the payload length and create the buffer
                    if (LengthSize < length)
                    {
                        _payloadlength = BitConverter.ToInt32(buffer, localOffset);
                        _buffer = new byte[_payloadlength];
                        _lengthBufferOffset = LengthSize;
                        bytesCopied += LengthSize;
                        localOffset += LengthSize;
                    }
                    else
                    {
                        //check if we are copying the length buffer
                        //copy the given buffer into the length buffer, because the first 4 bytes are the length of the payload
                        System.Buffer.BlockCopy(buffer, localOffset, _lengthBuffer, 0, length);
                        bytesCopied += length;
                        _lengthBufferOffset = length;

                        if (_lengthBufferOffset == LengthSize)
                        {
                            _payloadlength = BitConverter.ToInt32(_lengthBuffer, 0);
                            _buffer = new byte[_payloadlength];
                        }

                        return isComplete = false;
                    }
                }
            }

            int remainingBufferBytes = length - (localOffset - offset);

            //do we have any remaing bytes to read 
            if (remainingBufferBytes > 0)
            {
                //is the remaining big enough to contain the rest of the payload
                int missingPayloadBytes = _payloadlength - _offset;

                int numOfBytesToCopy = missingPayloadBytes < remainingBufferBytes ? missingPayloadBytes : remainingBufferBytes;

                System.Buffer.BlockCopy(buffer, localOffset, _buffer, _offset, numOfBytesToCopy);
                bytesCopied += numOfBytesToCopy;
                _offset += numOfBytesToCopy;
            }

            return _offset == _payloadlength;
        }

        public byte[] Build()
        {
            _buffer = new byte[Size.Length + BodyDataType.Length + Body.Length];

            //if the whole payload uses the same buffer return the buffer

            using (MemoryStream ms = new MemoryStream(_buffer.Length))
            {
                ms.Write(Size.Buffer, Size.Offset, Size.Length);

                ms.Write(BodyDataType.Size.Buffer, BodyDataType.Size.Offset, BodyDataType.Size.Buffer.Length);
                ms.Write(BodyDataType.Data.Buffer, BodyDataType.Data.Offset, BodyDataType.Data.Buffer.Length);

                ms.Write(Body.Size.Buffer, Body.Size.Offset, Body.Size.Buffer.Length);
                ms.Write(Body.Data.Buffer, Body.Data.Offset, Body.Data.Buffer.Length);

                ms.Seek(0, SeekOrigin.Begin);
                ms.Read(_buffer, 0, _buffer.Length);
            }

            //int destOffset = 0;
            //System.Buffer.BlockCopy(Size.Buffer, 0, _buffer, destOffset, Size.Length);
            //destOffset += Size.Length;

            //System.Buffer.BlockCopy(DataType.Size.Buffer, 0, _buffer, destOffset, DataType.Size.Length);
            //destOffset += DataType.Size.Length;

            //System.Buffer.BlockCopy(DataType.Data.Buffer, 0, _buffer, destOffset, DataType.Data.Length);
            //destOffset += DataType.Data.Length;

            //System.Buffer.BlockCopy(Data.Size.Buffer, 0, _buffer, destOffset, Data.Size.Length);
            //destOffset += Data.Size.Length;

            //System.Buffer.BlockCopy(Data.Data.Buffer, 0, _buffer, destOffset, Data.Data.Length);
            //destOffset += Data.Data.Length;

            //Debug.Assert(destOffset == _buffer.Length);

            return _buffer;
        }

        public void Reset()
        {
            _buffer = null;
            _offset = 0;
            _payloadlength = 0;
            _lengthBufferOffset = 0;
        }
    }
}