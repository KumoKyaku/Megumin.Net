using System;
using System.Buffers.Binary;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Megumin.Message
{
    /// <summary>
    /// 小端
    /// </summary>

    public static class SpanByteExtension_37AAF334E75041368C6B47A256F0F93F
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

        public static (int RpcID, short CMD, int MessageID) ReadHeader(this byte[] byteSequence)
        {
            unsafe
            {
                Span<byte> span = byteSequence;
                var rpcID = span.ReadInt();
                var cmd = span.Slice(4).ReadShort();
                var msgID = span.Slice(6).ReadInt();
                return (rpcID, cmd, msgID);
            }
        }

        public static (int RpcID, short CMD, int MessageID) ReadHeader(this in ReadOnlySpan<byte> byteSequence)
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

        public static (int RpcID, short CMD, int MessageID) ReadHeader(this in ReadOnlyMemory<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[10];
                byteSequence.Span.Slice(0, 10).CopyTo(span);
                var rpcID = span.ReadInt();
                var cmd = span.Slice(4).ReadShort();
                var msgID = span.Slice(6).ReadInt();
                return (rpcID, cmd, msgID);
            }
        }

        #region Int

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                source.Slice(start, 4).CopyTo(span);
                return span.ReadInt();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this Memory<byte> source)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(source.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this ReadOnlySpan<byte> source)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this ReadOnlyMemory<byte> source)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(source.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this Span<byte> source)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(source);
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
        public static long ReadLong(this in ReadOnlySequence<byte> source)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[8];
                source.Slice(0, 8).CopyTo(span);
                return span.ReadLong();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this Memory<byte> source)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(source.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this ReadOnlySpan<byte> source)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this ReadOnlyMemory<byte> source)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(source.Span);
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
        public static float ReadFloat(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[4];
                source.Slice(start, 4).CopyTo(span);
                return span.ReadFloat();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(this Span<byte> source)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadSingleLittleEndian(source);
#else
            return BitConverter.ToSingle(source.ToArray(), 0);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(this in ReadOnlySpan<byte> source)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadSingleLittleEndian(source);
#else
            return BitConverter.ToSingle(source.ToArray(), 0);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(this in ReadOnlyMemory<byte> source)
        {
            return source.Span.ReadFloat();
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
        public static double ReadDouble(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[8];
                source.Slice(start, 8).CopyTo(span);
                return span.ReadDouble();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this Span<byte> source)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadDoubleLittleEndian(source);
#else
            return BitConverter.ToDouble(source.ToArray(), 0);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this in ReadOnlySpan<byte> source)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadDoubleLittleEndian(source);
#else
            return BitConverter.ToDouble(source.ToArray(), 0);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this in ReadOnlyMemory<byte> source)
        {
            return source.Span.ReadDouble();
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
        public static short ReadShort(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[2];
                source.Slice(start, 2).CopyTo(span);
                return span.ReadShort();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this ReadOnlyMemory<byte> source)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(source.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this Span<byte> source)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this ReadOnlySpan<byte> source)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(source);
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
        public static ushort ReadUShort(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[2];
                source.Slice(start, 2).CopyTo(span);
                return span.ReadUShort();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort(this ReadOnlyMemory<byte> source)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(source.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort(this Span<byte> source)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort(this ReadOnlySpan<byte> source)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this ushort num, Span<byte> span)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span, num);
            return 2;
        }

        #endregion

        #region GUID

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[16];
                source.Slice(start, 16).CopyTo(span);
                return span.ReadGuid();
            }
        }

        /// <summary>
        /// todo 优化alloc
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this ReadOnlySpan<byte> source)
        {
            if (source.Length < 16)
            {
                return default;
            }

            byte[] temp = new byte[16];
            source.Slice(0, 16).CopyTo(temp);
            return new Guid(temp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this Span<byte> source)
        {
            return ReadGuid((ReadOnlySpan<byte>)source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this in Guid guid, Span<byte> target)
        {
            var temp = guid.ToByteArray();
            temp.AsSpan().CopyTo(target);
            return 16;
        }

        #endregion

        #region bool?

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(this Span<byte> span, bool? value)
        {
            if (value == false)
            {
                span[0] = 0;
            }
            else if (value == true)
            {
                span[0] = 1;
            }
            else
            {
                span[0] = 255;
            }
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool? ReadBoolNullable(this in ReadOnlySequence<byte> source, int start = 0)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[1];
                source.Slice(start, 1).CopyTo(span);
                return ReadBoolNullable((ReadOnlySpan<byte>)span);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool? ReadBoolNullable(this in ReadOnlySpan<byte> source)
        {
            unsafe
            {
                unsafe
                {
                    byte flag = source[0];
                    if (flag == 255)
                    {
                        return null;
                    }
                    else if (flag == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool? ReadBoolNullable(this in Span<byte> source)
        {
            return ReadBoolNullable((ReadOnlySpan<byte>)source);
        }

        #endregion
    }
}




