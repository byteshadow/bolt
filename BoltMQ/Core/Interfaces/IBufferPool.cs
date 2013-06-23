using System.Net.Sockets;

namespace BoltMQ.Core.Interfaces
{
    // This class creates a single large buffer which can be divided up  
    // and assigned to SocketAsyncEventArgs objects for use with each  
    // socket I/O operation.   
    // This enables bufffers to be easily reused and guards against  
    // fragmenting heap memory. 
    //  
    // The operations exposed on the BufferManager class are not thread safe. 
    internal interface IBufferPool
    {
        //Total size if the Pool
        int TotalSize { get; }

        //The size of each segment
        int SegmentSize { get; }

        //The number of segments in the pool
        int Count { get; }

        // Assigns a buffer from the buffer pool to the  
        // specified SocketAsyncEventArgs object 
        // 
        // <returns>true if the buffer was successfully set, else false</returns> 
        bool SetBuffer(SocketAsyncEventArgs args);

        // Removes the buffer from a SocketAsyncEventArg object.   
        // This frees the buffer back to the buffer pool 
        void FreeBuffer(SocketAsyncEventArgs args);
    }
}