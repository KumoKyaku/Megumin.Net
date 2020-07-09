using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message.Test
{
    /// <summary>
    /// 序列化后长度为1024字节
    /// </summary>
    public class TestPacket1: IMeguminFormater
    {
        private const int Size = 1024;

        public int Value { get; set; }
        ///<inheritdoc/>
        public int MessageID { get; } = 1000;
        public Type BindType => this.GetType();

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            var message = (TestPacket1)value;
            var buffer = writer.GetSpan(Size);
            BinaryPrimitives.WriteInt32LittleEndian(buffer, message.Value);
            writer.Advance(Size);
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            if (byteSequence.Length < Size)
            {
                return null;
            }

            var result = new TestPacket1();
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                byteSequence.Slice(0,4).CopyTo(span);
                result.Value = BinaryPrimitives.ReadInt32LittleEndian(span);
            }
            return result;
        }
    }

    /// <summary>
    /// 序列化后长度为1024字节
    /// </summary>
    public class TestPacket2 : IMeguminFormater
    {
        private const int Size = 1024;

        public float Value { get; set; }
        ///<inheritdoc/>
        public int MessageID { get; } = 1001;
        public Type BindType => this.GetType();

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            var message = (TestPacket2)value;
            var buffer = writer.GetSpan(Size);
            BitConverter.GetBytes(message.Value).AsSpan().CopyTo(buffer);
            writer.Advance(Size);
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            if (byteSequence.Length < Size)
            {
                return null;
            }

            var result = new TestPacket2();
            unsafe
            {
                byte[] span = new byte[4];
                byteSequence.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }
    }
}
