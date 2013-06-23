using BoltMQ.Core.Collection;

namespace BoltMQ
{
    public class StreamBufferItem : IRingBufferItem<byte[]>
    {
        public StreamBufferItem(int i)
        {
            Index = i;
        }

        #region Implementation of IRingBufferItem<byte[]>

        public int Index { get; private set; }
        public byte[] Item { get; set; }

        public int BufferLength { get; set; }

        #endregion
    }
}