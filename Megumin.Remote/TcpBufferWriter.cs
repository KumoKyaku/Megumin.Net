﻿using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Megumin.Message;

namespace Megumin.Remote
{
    /// <summary>
    /// 要发送/接收的字节块
    /// </summary>
    public interface IBufferBlock
    {
        /// <summary>
        /// 发送/接收 成功,释放内存块。
        /// </summary>
        void Free();
        /// <summary>
        /// 要 发送/接收 的内存块
        /// </summary>
        ReadOnlyMemory<byte> BlockMemory { get; }
        /// <summary>
        /// 要 发送/接收 的内存块
        /// </summary>
        ArraySegment<byte> BlockSegment { get; }
    }

    /// <summary>
    /// 消息字节写入器
    /// </summary>
    public interface ITransportWriter : IBufferWriter<byte>
    {
        /// <summary>
        /// 放弃发送，废弃当前写入器
        /// </summary>
        void Discard();
    }

    public abstract class BaseBufferWriter : IBufferWriter<byte>, IBufferBlock, ITransportWriter
    {
        protected int defaultCount = 1024 * 8;
        protected byte[] buffer;
        /// <summary>
        /// 当前游标位置
        /// </summary>
        protected int index = 0;
        protected readonly object syncLock = new object();
        public ReadOnlyMemory<byte> BlockMemory => new ReadOnlyMemory<byte>(buffer, 0, index);
        public ArraySegment<byte> BlockSegment => new ArraySegment<byte>(buffer, 0, index);

        public void Advance(int count)
        {
            index += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            lock (syncLock)
            {
                Ensure(sizeHint);
                return new Memory<byte>(buffer, index, buffer.Length - index);
            }
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            lock (syncLock)
            {
                Ensure(sizeHint);
                return new Span<byte>(buffer, index, buffer.Length - index);
            }
        }

        /// <summary>
        /// 确保当前buffer足够大
        /// </summary>
        /// <param name="sizeHint"></param>
        protected virtual void Ensure(int sizeHint)
        {
            if (buffer == null)
            {
                var size = Math.Max(sizeHint, defaultCount);
                buffer = ArrayPool<byte>.Shared.Rent(size);
                if (buffer == null)
                {
                    //内存池用尽.todo 这里应该有个log
                    Console.WriteLine($" ArrayPool<byte>.Shared.Rent(size) 返回null");
                    buffer = new byte[size];
                }
                return;
            }

            var leftLength = buffer.Length - index;
            if (leftLength >= sizeHint && leftLength > 0)
            {
                //现有数组足够长；
            }
            else
            {
                //扩容 每次增加4096比较合适，可根据生产环境修改。
                var newCount = ((buffer.Length + sizeHint) / 4096 + 1) * 4096;
                var newbuffer = ArrayPool<byte>.Shared.Rent(newCount);
                buffer.AsSpan().CopyTo(newbuffer);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = newbuffer;
            }
        }

        protected abstract void Reset();

        public void Discard()
        {
            Reset();
        }

        public void Free()
        {
            Reset();
        }
    }

    /// <summary>
    /// 起始位置总是预留4个字节，写入消息总长度
    /// </summary>
    public class TcpBufferWriter : BaseBufferWriter
    {
        public TcpBufferWriter(int bufferLenght = 1024 * 8)
        {
            this.defaultCount = bufferLenght;
            Reset();
        }

        protected override void Reset()
        {
            lock (syncLock)
            {
                index = 4;

                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                }
            }
        }

        public int WriteLengthOnHeader()
        {
            var len = index;
            buffer.AsSpan().Write(index);//在起始位置写入长度
            return len;
        }
    }

    /// <summary>
    /// Tcp发送管道 存在并发/异步函数重入问题
    /// </summary>
    public class TcpSendPipe
    {
        public int DefaultWriterSize { get; set; } = 8192;

        ConcurrentQueue<IBufferBlock> sendQueue = new ConcurrentQueue<IBufferBlock>();

        protected readonly object _pushLock = new object();

        internal protected void Push2Queue(IBufferBlock sendblock)
        {
            lock (_pushLock)
            {
                sendQueue.Enqueue(sendblock);
                if (sendQueue.TryPeek(out var block))
                {
                    if (source != null)
                    {
                        var curSource = source;
                        source = null;
                        curSource?.SetResult(block);
                    }
                }
            }
        }

        /// <summary>
        /// 取得一个可用写入器
        /// 每个writer都是一个新的实例，保证序列化时缓冲区线程安全。
        /// </summary>
        /// <returns></returns>
        internal TcpBufferWriter GetWriter()
        {
            return new TcpBufferWriter(DefaultWriterSize);
        }

        TaskCompletionSource<IBufferBlock> source;

        /// <summary>
        /// 取得下一个待发送消息。
        /// </summary>
        /// <returns></returns>
        public ValueTask<IBufferBlock> ReadNext()
        {
            if (source != null)
            {
                throw new Exception($"ReadSendPipe 重复挂起，同一时刻只能有一出发送，这里不符合预期"); //todo 已经有个发送任务在等了。
            }

            lock (_pushLock)
            {
                if (sendQueue.TryPeek(out var block))
                {
                    return new ValueTask<IBufferBlock>(block);
                }
                else
                {
                    //todo IValueTaskSource
                    source = new TaskCompletionSource<IBufferBlock>();
                    return new ValueTask<IBufferBlock>(source.Task);
                }
            }
        }

        public bool Advance(IBufferBlock sendBlock)
        {
            if (sendQueue.TryDequeue(out var first))
            {
                if (first != sendBlock)
                {
                    throw new InvalidOperationException($"发送队列出现排序错误");
                }
                sendBlock.Free();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 取消正在挂起的ReadNext
        /// </summary>
        /// <remarks>
        /// 会导致Socket发送循环中断，需要重新调用ReadSendPipe。
        /// 用于断线重连时，将旧的Remote的SendPipe，赋值给新的Remote，并取消旧的Remote的pending。
        /// </remarks>
        public void CancelPendingRead()
        {
            source = null;
        }
    }
}
