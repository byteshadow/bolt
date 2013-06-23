using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BoltMQ;
using BoltMQ.Core.Interfaces;
using ProtoBuf;

namespace Shared
{
    [Message(1)]
    [ProtoContract]
    public class RequestMessage : IMessage
    {
        [ProtoMember(1)]
        public int Integer { get; set; }

        [ProtoMember(2)]
        public float Float { get; set; }

        [ProtoMember(3)]
        public string String { get; set; }

        [ProtoMember(4)]
        public bool Boolean { get; set; }
    }
}
