using System;
using System.Buffers;
using System.IO;

namespace Megumin.Remote
{
    /// <summary>
    /// 包装<see cref="IBufferWriter{T}"/><see cref="byte"/>成一个长度无限的只写流，
    /// 只有<see cref="Write(byte[], int, int)"/>函数起作用。
    /// </summary>
    public class BufferWriterBytesStream : Stream
    {
        public IBufferWriter<byte> BufferWriter { get; set; }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var destination = BufferWriter.GetSpan(count);
            var span = new Span<byte>(buffer, offset, count);
            span.CopyTo(destination);
            BufferWriter.Advance(count);
        }

        public override bool CanRead { get; } = false;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = true;
        public override long Length { get; } = long.MaxValue;
        public override long Position { get; set; }
    }
}


