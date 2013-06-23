namespace BoltMQ
{
    public class Frame
    {
        public Frame(byte[] buffer, int offset, int length)
        {
            Size = new FrameSegement { Buffer = buffer, Length = sizeof(int), Offset = offset };
            Data = new FrameSegement { Buffer = buffer, Length = length - sizeof(int), Offset = offset + sizeof(int) };
        }

        public Frame()
        {
            Size = new FrameSegement();
            Data = new FrameSegement();
            
        }

        public FrameSegement Size { get; set; }
        public FrameSegement Data { get; set; }
        public int Length { get { return Size.Length + Data.Length; } }
    }
}