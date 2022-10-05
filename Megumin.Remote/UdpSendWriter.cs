using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
{
    public class UdpSendWriter : IBufferWriter<byte>
    {
        private int defaultCount;
        private IMemoryOwner<byte> buffer;
        int index = 0;

        public UdpSendWriter(int bufferLenght)
        {
            this.defaultCount = bufferLenght;
        }

        /// <summary>
        /// 弹出一个序列化完毕的缓冲。
        /// </summary>
        /// <returns></returns>
        public (IMemoryOwner<byte>, int) Pop()
        {
            var old = buffer;
            var lenght = index;
            buffer = null;
            index = 0;
            return (old, lenght);
        }

        public void Advance(int count)
        {
            index += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            lock (syncLock)
            {
                Ensure(sizeHint);
                return buffer.Memory.Slice(index, buffer.Memory.Length - index);
            }

        }

        readonly object syncLock = new object();
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            lock (syncLock)
            {
                Ensure(sizeHint);
                return buffer.Memory.Span.Slice(index, buffer.Memory.Length - index);
            }
        }

        /// <summary>
        /// 确保当前buffer足够大
        /// </summary>
        /// <param name="sizeHint"></param>
        void Ensure(int sizeHint)
        {
            if (buffer == null)
            {
                var size = Math.Max(sizeHint, defaultCount);
                buffer = MemoryPool<byte>.Shared.Rent(size);
                return;
            }

            var leftLength = buffer.Memory.Length - index;
            if (leftLength >= sizeHint && leftLength > 0)
            {
                //现有数组足够长；
            }
            else
            {
                //扩容 每次增加4096比较合适，可根据生产环境修改。
                var newCount = ((buffer.Memory.Length + sizeHint) / 4096 + 1) * 4096;
                var newbuffer = MemoryPool<byte>.Shared.Rent(newCount);
                buffer.Memory.CopyTo(newbuffer.Memory);
                buffer.Dispose();
                buffer = newbuffer;
            }
        }

        public void WriteHeader(byte header)
        {
            var span = GetSpan(1);
            span[0] = header;
            Advance(1);
        }
    }
}




