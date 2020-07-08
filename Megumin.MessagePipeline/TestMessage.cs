using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message.TestMessage
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
            message.Value.WriteTo(buffer);
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
                result.Value = span.ReadInt();
            }
            return result;
        }
    }

    public class TestPacket2
    {
        public float Value { get; set; }

        public static ushort S(TestPacket2 message, Span<byte> buffer)
        {
            BitConverter.GetBytes(message.Value).AsSpan().CopyTo(buffer);
            return 1000;
        }

        public static TestPacket2 D(ReadOnlyMemory<byte> buffer)
        {
            var res = new TestPacket2();
            var temp = new byte[4];
            buffer.Span.Slice(0, 4).CopyTo(temp);
            res.Value = BitConverter.ToSingle(temp,0);
            return res;
        }
    }
}
