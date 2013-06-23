using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using BoltMQ.Core.Interfaces;
using System.Reactive.Linq.Observαble;

namespace BoltMQ
{
    public class BlockingStreamHandler : IStreamHandler
    {
        private readonly PayloadParser _payloadParser;
        private readonly IMessageProcessor _messageProcessor;
        private readonly Guid _sessionId;
        private readonly BufferManager _bufferManager;
        private readonly BlockingCollection<Tuple<byte[], int>> _streamBufferCollection = new BlockingCollection<Tuple<byte[], int>>(64000);

        public Guid SessionId { get; private set; }
        public Exception StreamHandlerException { get; private set; }

        public PayloadParser CurrentPayloadParser
        {
            get { return _payloadParser; }
        }

        public BlockingStreamHandler(IMessageProcessor messageProcessor, Guid sessionId)
        {
            SessionId = sessionId;
            _payloadParser = new PayloadParser();
            _messageProcessor = messageProcessor;
            _sessionId = sessionId;
            _bufferManager = BufferManager.CreateBufferManager(64000, 8192);

            Task.Factory.StartNew(() =>
                {
                    foreach (var bytes in _streamBufferCollection.GetConsumingEnumerable())
                    {
                        ParseStream(bytes.Item1, 0, bytes.Item2);
                        _bufferManager.ReturnBuffer(bytes.Item1);
                    }
                }, TaskCreationOptions.LongRunning);

        }

        public void CopyStreamBuffer(byte[] buffer, int offset, int length)
        {
            var localBuffer = _bufferManager.TakeBuffer(length);
            Buffer.BlockCopy(buffer, offset, localBuffer, 0, length);
            _streamBufferCollection.Add(Tuple.Create(localBuffer, length));
        }


        public bool ParseStream(byte[] buffer, int offset, int length)
        {
            int initialOffset = offset;
            int currentOffset = offset;
            int remainingBytes = length - (currentOffset - initialOffset);

            try
            {
                while (remainingBytes > 0)
                {
                    int bytesCopied;
                    bool isComplete = _payloadParser.Write(buffer, currentOffset, remainingBytes, out bytesCopied);

                    if (isComplete)
                    {
                        _messageProcessor.Process(_payloadParser.Buffer, _sessionId);
                        _payloadParser.Reset();
                    }

                    currentOffset += bytesCopied;
                    remainingBytes = length - (currentOffset - initialOffset);
                }
                return true;
            }
            catch (Exception ex)
            {
                StreamHandlerException = ex;
                return false;
            }
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            _streamBufferCollection.CompleteAdding();
        }

        #endregion
    }
}