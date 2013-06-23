using System;
using System.Diagnostics;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public class StreamHandler : Disposable, IStreamHandler
    {
        private readonly IMessageProcessor _messageProcessor;
        private readonly PayloadParser _payloadParser;

        public StreamHandler(IMessageProcessor messageProcessor, Guid sessionId)
        {
            _messageProcessor = messageProcessor;
            SessionId = sessionId;
            _payloadParser = new PayloadParser();
        }

        public Guid SessionId { get; private set; }

        public Exception StreamHandlerException { get; private set; }

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
                        _messageProcessor.Process(_payloadParser.Buffer, SessionId);
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
                Trace.TraceError("{0}{1}", ex.Message, ex.StackTrace);
                return false;
            }
        }

        public override void OnDispose() { }
    }
}