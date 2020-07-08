using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace UnitTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestBYTE()
        {
            byte a = 255;
            sbyte b = (sbyte)a;
            Assert.AreEqual(-1, b);
            a = 0;
            b = (sbyte)a;
            Assert.AreEqual(0, b);
            a = 1;
            b = (sbyte)a;
            Assert.AreEqual(1, b);
            byte[] buffer = new byte[4];
            255.WriteTo(buffer);
            256.WriteTo(buffer);
            65535.WriteTo(buffer);
            65536.WriteTo(buffer);
        }
    }
}


namespace System.Buffers
{
    /// <summary>
    /// 小端
    /// </summary>
    public static class SpanByteEX_3451DB8C29134366946FF9D778779EEC
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="num"></param>
        /// <param name="span"></param>
        /// <returns>offset</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTo(this int num, Span<byte> span)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span, num);
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTo(this ushort num, Span<byte> span)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span, num);
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTo(this short num, Span<byte> span)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span, num);
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTo(this long num, Span<byte> span)
        {
            BinaryPrimitives.WriteInt64LittleEndian(span, num);
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt32LittleEndian(span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadUInt16LittleEndian(span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt16LittleEndian(span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt64LittleEndian(span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this Span<byte> span)
            => BinaryPrimitives.ReadInt32LittleEndian(span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this Span<byte> span)
            => BinaryPrimitives.ReadUInt16LittleEndian(span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this Span<byte> span)
            => BinaryPrimitives.ReadInt16LittleEndian(span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this Span<byte> span)
            => BinaryPrimitives.ReadInt64LittleEndian(span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this Memory<byte> span)
            => BinaryPrimitives.ReadInt32LittleEndian(span.Span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this Memory<byte> span)
            => BinaryPrimitives.ReadUInt16LittleEndian(span.Span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this Memory<byte> span)
            => BinaryPrimitives.ReadInt16LittleEndian(span.Span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this Memory<byte> span)
            => BinaryPrimitives.ReadInt64LittleEndian(span.Span);
    }
}

