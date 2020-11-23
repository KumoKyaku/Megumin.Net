﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// 小端
/// </summary>
internal static class SpanByteExtension_37AAF334E75041368C6B47A256F0F93F
{
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

    public static (int RpcID, short CMD, int MessageID)
        ReadHeader(this in ReadOnlySequence<byte> byteSequence)
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
    public static int WriteTo(this in Guid guid, Span<byte> target)
    {
        var temp = guid.ToByteArray();
        temp.AsSpan().CopyTo(target);
        return 16;
    }
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

namespace Megumin.Remote
{
    /// <summary>
    /// 主动还是被动
    /// </summary>
    public enum ActiveOrPassive
    {
        /// <summary>
        /// 主动的
        /// </summary>
        Active,
        /// <summary>
        /// 被动的
        /// </summary>
        Passive,
    }

    /// <summary>
    /// 记录器
    /// </summary>
    public interface IMeguminRemoteLogger
    {
        void Log(string error);
    }

    /// <summary>
    /// 事实上 无论UID是Int,long,还是string,都无法满足全部需求。当你需要其他类型是，请修改源码。
    /// </summary>
    public interface IRemoteUID<T>
    {
        /// <summary>
        /// 预留给用户使用的ID，（用户自己赋值ID，自己管理引用，框架不做处理）
        /// </summary>
        T UID { get; set; }
    }

    public class Utility
    {
    }
}
