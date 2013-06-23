using System;
using System.ComponentModel.Composition;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MessageAttribute : ExportAttribute
    {
        private readonly ushort _id;

        public MessageAttribute(ushort id)
            : base(typeof(IMessage))
        {
            _id = id;
        }

        public ushort Id
        {
            get { return _id; }
        }
    }
}
