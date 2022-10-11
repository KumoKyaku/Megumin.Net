using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    public class MessageLUTTestBuffer : IBufferWriter<byte>
    {
        const int DefaultLength = 1024 * 100;
        byte[] data = new byte[DefaultLength];
        int index = 0;
        public void Advance(int count)
        {
            index += count;
        }

        protected void Ensure(int sizeHint)
        {
            var leftLength = data.Length - index;
            if (leftLength >= sizeHint && leftLength > 0)
            {
                var newData = new byte[data.Length + sizeHint + DefaultLength];
                data.CopyTo(newData, 0);
                data = newData;
            }
        }

        public void Reset()
        {
            index = 0;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return new Memory<byte>(data, index, data.Length - index);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return new Span<byte>(data, index, data.Length - index);
        }

        public ReadOnlyMemory<byte> ReadOnlyMemory => new ReadOnlyMemory<byte>(data, 0, index);
        public ReadOnlySpan<byte> ReadOnlySpan => ReadOnlyMemory.Span;
        public ReadOnlySequence<byte> ReadOnlySequence => new ReadOnlySequence<byte>(data, 0, index);
    }
}
