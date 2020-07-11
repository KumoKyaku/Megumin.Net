﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

/// <summary>
/// 小端
/// </summary>
internal static class SpanByteEX_C9701074
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt(this in ReadOnlySequence<byte> byteSequence)
    {
        unsafe
        {
            Span<byte> span = stackalloc byte[4];
            byteSequence.CopyTo(span);
            return span.ReadInt();
        }
    }

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


/// <summary>
/// 线程安全ID生成器
/// </summary>
/// <typeparam name="T"></typeparam>
internal static class InterlockedID<T>
{
    static int id = 0;
    static readonly object locker = new object();
    public static int NewID(int min = 0)
    {
        lock (locker)
        {
            if (id < min)
            {
                id = min;
                return id;
            }

            return id++;
        }
    }
}
