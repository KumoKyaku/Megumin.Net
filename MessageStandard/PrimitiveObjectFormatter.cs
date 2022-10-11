using Megumin.Remote;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Megumin.Message
{
    /// <summary>
    /// 内置UTF8 string 格式化器，性能低没有优化
    /// </summary>
    internal class StringFormatter : IMeguminFormater<string>
    {
        internal static readonly Encoding UTF8 = new UTF8Encoding(false);
        public void Serialize(IBufferWriter<byte> writer, string value, object options = null)
        {
            var bytes = UTF8.GetBytes(value);
            writer.Write(bytes);
        }

        public int MessageID => MSGID.String;
        public Type BindType => typeof(string);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, value.ToString(), options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
#if NET5_0_OR_GREATER
            return UTF8.GetString(source);
#else
            return UTF8.GetString(source.ToArray());
#endif
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
#if NET5_0_OR_GREATER
            return UTF8.GetString(source);
#else
            return UTF8.GetString(source.ToArray());
#endif
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
#if NET5_0_OR_GREATER
            return UTF8.GetString(source.Span);
#else
            return UTF8.GetString(source.ToArray());
#endif
        }
    }

    internal class IntFormatter : IMeguminFormater<int>
    {
        public void Serialize(IBufferWriter<byte> writer, int value, object options = null)
        {
            var span = writer.GetSpan(4);
            span.Write(value);
            writer.Advance(4);
        }

        public int MessageID => MSGID.Int32;
        public Type BindType => typeof(int);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (int)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return source.ReadInt();
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return source.ReadInt();
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return source.ReadInt();
        }
    }

    internal class FloatFormatter : IMeguminFormater<float>
    {
        public void Serialize(IBufferWriter<byte> writer, float value, object options = null)
        {
            var span = writer.GetSpan(4);
            span.Write(value);
            writer.Advance(4);
        }

        public int MessageID => MSGID.Single;
        public Type BindType => typeof(float);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (float)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return source.ReadFloat();
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return source.ReadFloat();
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return source.ReadFloat();
        }
    }

    internal class LongFormatter : IMeguminFormater<long>
    {
        public void Serialize(IBufferWriter<byte> writer, long value, object options = null)
        {
            var span = writer.GetSpan(8);
            span.Write(value);
            writer.Advance(8);
        }

        public int MessageID => MSGID.Int64;
        public Type BindType => typeof(long);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (long)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return source.ReadLong();
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return source.ReadLong();
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return source.ReadLong();
        }
    }

    internal class DoubleFormatter : IMeguminFormater<double>
    {
        public void Serialize(IBufferWriter<byte> writer, double value, object options = null)
        {
            var span = writer.GetSpan(8);
            span.Write(value);
            writer.Advance(8);
        }

        public int MessageID => MSGID.Double;
        public Type BindType => typeof(double);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (double)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return source.ReadDouble();
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return source.ReadDouble();
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return source.ReadDouble();
        }
    }

    internal class DatetimeFormatter : IMeguminFormater<DateTime>
    {
        public void Serialize(IBufferWriter<byte> writer, DateTime value, object options = null)
        {
            var span = writer.GetSpan(8);
            span.Write(value.ToBinary());
            writer.Advance(8);
        }

        public int MessageID => MSGID.DateTime;
        public Type BindType => typeof(DateTime);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (DateTimeOffset)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return DateTime.FromBinary(source.ReadLong());
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return DateTime.FromBinary(source.ReadLong());
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return DateTime.FromBinary(source.ReadLong());
        }
    }

    internal class DatetimeOffsetFormatter : IMeguminFormater<DateTimeOffset>
    {
        public void Serialize(IBufferWriter<byte> writer, DateTimeOffset value, object options = null)
        {
            var span = writer.GetSpan(8);
            span.Write(value.ToUnixTimeMilliseconds());
            writer.Advance(8);
        }

        public int MessageID => MSGID.DateTimeOffset;
        public Type BindType => typeof(DateTimeOffset);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (DateTimeOffset)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(source.ReadLong());
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(source.ReadLong());
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(source.ReadLong());
        }
    }

    internal class ByteArrayFormatter : IMeguminFormater<byte[]>
    {
        public void Serialize(IBufferWriter<byte> writer, byte[] value, object options = null)
        {
            writer.Write(value);
        }

        public int MessageID => MSGID.ByteArray;
        public Type BindType => typeof(byte[]);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (byte[])value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return source.ToArray();
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return source.ToArray();
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return source.ToArray();
        }
    }
}
