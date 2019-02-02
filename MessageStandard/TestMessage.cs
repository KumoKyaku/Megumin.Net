using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message.TestMessage
{
    public class TestPacket1
    {
        public int Value { get; set; }

        public static ushort S(TestPacket1 message, Span<byte> buffer)
        {
            message.Value.WriteTo(buffer);
            return 1000;
        }

        public static TestPacket1 D(ReadOnlyMemory<byte> buffer)
        {
            var res = new TestPacket1();
            res.Value = buffer.Span.ReadInt();
            return res;
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
