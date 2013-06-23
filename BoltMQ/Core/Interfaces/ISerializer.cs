using System;

namespace BoltMQ.Core.Interfaces
{
    public interface ISerializer : IDisposable
    {
        object Deserialize(byte[] payload);
        byte[] Serialize<T>(T entity);
    }
}