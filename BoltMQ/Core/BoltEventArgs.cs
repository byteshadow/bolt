using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltMQ.Core
{
    public class BoltEventArgs<T> : EventArgs
    {
        private readonly Guid _sessionId;
        private readonly T _message;

        public Guid SessionId
        {
            get { return _sessionId; }
        }

        public T Message
        {
            get { return _message; }
        }

        public BoltEventArgs(Guid sessionId, T message)
        {
            _sessionId = sessionId;
            _message = message;
        }
    }
}
