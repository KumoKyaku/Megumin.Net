using Megumin.Remote;
using System;
using System.Buffers.Binary;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.IO;

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

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket1();
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BinaryPrimitives.ReadInt32LittleEndian(span);
            }
            return result;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket1();
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BinaryPrimitives.ReadInt32LittleEndian(span);
            }
            return result;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket1();
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                source.Span.Slice(0, 4).CopyTo(span);
                result.Value = BinaryPrimitives.ReadInt32LittleEndian(span);
            }
            return result;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            throw new NotImplementedException();
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

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket2();
            unsafe
            {
                byte[] span = new byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket2();
            unsafe
            {
                byte[] span = new byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket2();
            unsafe
            {
                byte[] span = new byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            throw new NotImplementedException();
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

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket3();
            unsafe
            {
                byte[] span = new byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket3();
            unsafe
            {
                byte[] span = new byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            if (source.Length < Size)
            {
                return null;
            }

            var result = new TestPacket3();
            unsafe
            {
                byte[] span = new byte[4];
                source.Slice(0, 4).CopyTo(span);
                result.Value = BitConverter.ToSingle(span, 0);
            }
            return result;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            throw new NotImplementedException();
        }
    }

    public class TestPacket4 : IMeguminFormater, IMeguminFormater<TestPacket4>
    {
        public string StringValue { get; set; } = "Test String!!";
        public int Value { get; set; } = MSGID.TestPacket4;
        ///<inheritdoc/>
        public int MessageID => MSGID.TestPacket4;
        public Type BindType => this.GetType();

        public void Serialize(IBufferWriter<byte> writer, TestPacket4 value, object options = null)
        {
            MessageLUT.Serialize(writer, value.StringValue, options);
            MessageLUT.Serialize(writer, value.Value, options);
        }

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (TestPacket4)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            var myptions = DeserializeLengthHelper.Default;
            var str = MessageLUT.Deserialize<string>(source, DeserializeLengthHelper.Default);
            var intValue = source.Slice(myptions.Length, 4).ReadInt();
            if (options is IDeserializeLengthWriter writer)
            {
                writer.Length = myptions.Length + 4;
            }
            return new TestPacket4() { StringValue = str, Value = intValue };
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            var myptions = DeserializeLengthHelper.Default;
            var str = MessageLUT.Deserialize<string>(source, DeserializeLengthHelper.Default);
            var intValue = source.Slice(myptions.Length, 4).ReadInt();
            if (options is IDeserializeLengthWriter writer)
            {
                writer.Length = myptions.Length + 4;
            }
            return new TestPacket4() { StringValue = str, Value = intValue };
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            var myptions = DeserializeLengthHelper.Default;
            var str = MessageLUT.Deserialize<string>(source, DeserializeLengthHelper.Default);
            var intValue = source.Slice(myptions.Length, 4).ReadInt();
            if (options is IDeserializeLengthWriter writer)
            {
                writer.Length = myptions.Length + 4;
            }
            return new TestPacket4() { StringValue = str, Value = intValue };
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, TestPacket4 value, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            throw new NotImplementedException();
        }
    }
}
