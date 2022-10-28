using System;
using System.Buffers;

namespace Megumin.Remote
{
    public class UdpBufferWriter : BaseBufferWriter
    {
        public UdpBufferWriter(int bufferLenght = 0x10000)
        {
            this.defaultCount = bufferLenght;
            Reset();
        }

        public void WriteHeader(byte header)
        {
            var span = GetSpan(1);
            span[0] = header;
            Advance(1);
        }

        protected override void Reset()
        {
            lock (syncLock)
            {
                index = 0;

                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                }
            }
        }
    }

    public class ManagedMemoryOwner : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; }

        public ManagedMemoryOwner(int size)
        {
            Memory = new Memory<byte>(new byte[size]);
        }
        public void Dispose()
        {
        }
    }
}




