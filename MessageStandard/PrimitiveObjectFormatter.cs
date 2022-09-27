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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
#if NET5_0_OR_GREATER
            return UTF8.GetString(byteSequence);
#else
            return UTF8.GetString(byteSequence.ToArray());
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return byteSequence.ReadInt();
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return byteSequence.ReadFloat();
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return byteSequence.ReadLong();
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return byteSequence.ReadDouble();
        }
    }

    /// <summary>
    /// 小端
    /// </summary>
    internal static class SpanByteExtension_37AAF334E75041368C6B47A256F0F93F
    {
        public static (int RpcID, short CMD, int MessageID) ReadHeader(this in ReadOnlySequence<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[10];
                byteSequence.Slice(0, 10).CopyTo(span);
                var rpcID = span.ReadInt();
                var cmd = span.Slice(4).ReadShort();
                var msgID = span.Slice(6).ReadInt();
                return (rpcID, cmd, msgID);
            }
        }

        #region Int

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this in ReadOnlySequence<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                byteSequence.Slice(0, 4).CopyTo(span);
                return span.ReadInt();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this Memory<byte> span)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(span.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this Span<byte> span)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        /// <summary>
        /// 写入一个int
        /// </summary>
        /// <param name="num"></param>
        /// <param name="span"></param>
        /// <returns>offset</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this int num, Span<byte> span)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span, num);
            return 4;
        }

        /// <summary>
        /// 写入一个int
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this Span<byte> span, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span, value);
            return 4;
        }

        #endregion

        #region Long

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this in ReadOnlySequence<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[8];
                byteSequence.Slice(0, 8).CopyTo(span);
                return span.ReadLong();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this Memory<byte> span)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(span.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this Span<byte> span)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(span);
        }

        /// <summary>
        /// 写入一个long
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this Span<byte> span, long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(span, value);
            return 8;
        }

        /// <summary>
        /// 写入一个long
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this long value, Span<byte> span)
        {
            BinaryPrimitives.WriteInt64LittleEndian(span, value);
            return 8;
        }

        #endregion

        #region Float

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(this in ReadOnlySequence<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[8];
                byteSequence.Slice(0, 8).CopyTo(span);
                return span.ReadFloat();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(this Span<byte> span)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadSingleLittleEndian(span);
#else
            return BitConverter.ToSingle(span.ToArray(), 0);
#endif
        }

        /// <summary>
        /// 写入一个short
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this Span<byte> span, float value)
        {
#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteSingleLittleEndian(span, value);
#else
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(span);
#endif
            return 4;
        }

        /// <summary>
        /// 写入一个short
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this float value, Span<byte> span)
        {
#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteSingleLittleEndian(span, value);
#else
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(span);
#endif
            return 4;
        }

        #endregion

        #region Double

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this in ReadOnlySequence<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[8];
                byteSequence.Slice(0, 8).CopyTo(span);
                return span.ReadDouble();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this Span<byte> span)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadDoubleLittleEndian(span);
#else
            return BitConverter.ToDouble(span.ToArray(), 0);
#endif
        }

        /// <summary>
        /// 写入一个double
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this Span<byte> span, double value)
        {
#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteDoubleLittleEndian(span, value);
#else
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(span);
#endif
            return 8;
        }

        /// <summary>
        /// 写入一个double
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this double value, Span<byte> span)
        {
#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteDoubleLittleEndian(span, value);
#else
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(span);
#endif
            return 8;
        }

        #endregion

        #region Short

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this Memory<byte> span)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(span.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this Span<byte> span)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(span);
        }

        /// <summary>
        /// 写入一个short
        /// </summary>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this Span<byte> span, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span, value);
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this short num, Span<byte> span)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span, num);
            return 2;
        }

        #endregion

        #region UShort

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this Memory<byte> span)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(span.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this Span<byte> span)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this ushort num, Span<byte> span)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span, num);
            return 2;
        }

        #endregion

        #region GUID

        /// <summary>
        /// todo 优化alloc
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this ReadOnlySpan<byte> span)
        {
            if (span.Length < 16)
            {
                return default;
            }

            byte[] temp = new byte[16];
            span.Slice(0, 16).CopyTo(temp);
            return new Guid(temp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this Span<byte> span)
        {
            return ReadGuid((ReadOnlySpan<byte>)span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this in Guid guid, Span<byte> target)
        {
            var temp = guid.ToByteArray();
            temp.AsSpan().CopyTo(target);
            return 16;
        }

        #endregion
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return DateTime.FromBinary(byteSequence.ReadLong());
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(byteSequence.ReadLong());
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

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return byteSequence.ToArray();
        }
    }
}
