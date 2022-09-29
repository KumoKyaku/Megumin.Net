using Megumin.Remote;
using System;
using System.Buffers.Binary;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    /// <summary>
    /// 序列化后长度为1024 * 10字节
    /// </summary>
    public class TestPacket1 : IMeguminFormater
    {
        private const int Size = 1024 * 10;

        public int Value { get; set; }
        ///<inheritdoc/>
        public int MessageID => MSGID.TestPacket1;
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
                byteSequence.Slice(0, 4).CopyTo(span);
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
        public int MessageID => MSGID.TestPacket2;
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

    /// <summary>
    /// 序列化后长度为1024 * 50字节
    /// </summary>
    public class TestPacket3 : IMeguminFormater
    {
        private const int Size = 1024 * 50;

        public float Value { get; set; }
        ///<inheritdoc/>
        public int MessageID => MSGID.TestPacket3;
        public Type BindType => this.GetType();

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            var message = (TestPacket3)value;
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

            var result = new TestPacket3();
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
