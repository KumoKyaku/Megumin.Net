using Megumin.Message;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    /// <summary>
    /// 要发送的字节块
    /// </summary>
    public interface ISendBlock
    {
        /// <summary>
        /// 发送成功
        /// </summary>
        void SendSuccess();
        /// <summary>
        /// 要发送的内存块
        /// </summary>
        ReadOnlyMemory<byte> SendMemory { get; }
        /// <summary>
        /// 要发送的内存块
        /// </summary>
        ArraySegment<byte> SendSegment { get; }
    }

    /// <summary>
    /// 消息字节写入器
    /// </summary>
    public interface IWriter : IBufferWriter<byte>
    {
        /// <summary>
        /// 放弃发送，废弃当前写入器
        /// </summary>
        void Discard();
        /// <summary>
        /// 将总长度写入消息4位
        /// </summary>
        /// <returns>消息总长度</returns>
        int WriteLengthOnHeader();
    }

    /// <summary>
    /// Tcp发送管道 存在并发/异步函数重入问题
    /// </summary>
    public class TcpSendPipe
    {
        public int DefaultWriterSize { get; set; } = 8192;
        public class Writer : IBufferWriter<byte>, IWriter, ISendBlock
        {
            private TcpSendPipe sendPipe;
            private byte[] buffer;
            /// <summary>
            /// 当前游标位置
            /// </summary>
            int index = 4;
            readonly object syncLock = new object();
            public Writer(TcpSendPipe sendPipe)
            {
                this.sendPipe = sendPipe;
                Reset();
            }

            void Reset()
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
            void Ensure(int sizeHint)
            {
                if (buffer == null)
                {
                    var size = Math.Max(sizeHint, sendPipe.DefaultWriterSize);
                    buffer = ArrayPool<byte>.Shared.Rent(size);
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

            public void Discard()
            {
                Reset();
            }

            public int WriteLengthOnHeader()
            {
                var len = index;
                buffer.AsSpan().Write(index);//在起始位置写入长度
                sendPipe.Push2Queue(this);
                return len; //直接返回index可能会有多线程错误，Push2Queue后直接发送并释放，导致index重置。
            }

            public void SendSuccess()
            {
                Reset();
            }

            public ReadOnlyMemory<byte> SendMemory => new ReadOnlyMemory<byte>(buffer, 0, index);

            public ArraySegment<byte> SendSegment => new ArraySegment<byte>(buffer, 0, index);

        }

        ConcurrentQueue<ISendBlock> sendQueue = new ConcurrentQueue<ISendBlock>();

        protected readonly object _pushLock = new object();
        
        internal protected void Push2Queue(ISendBlock sendblock)
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
        /// </summary>
        /// <returns></returns>
        internal IWriter GetWriter()
        {
            return new Writer(this);
        }

        TaskCompletionSource<ISendBlock> source;

        /// <summary>
        /// 取得下一个待发送消息。
        /// </summary>
        /// <returns></returns>
        public ValueTask<ISendBlock> ReadNext()
        {
            if (source != null)
            {
                throw new Exception($"ReadSendPipe 重复挂起，同一时刻只能有一出发送，这里不符合预期"); //todo 已经有个发送任务在等了。
            }

            lock (_pushLock)
            {
                if (sendQueue.TryPeek(out var block))
                {
                    return new ValueTask<ISendBlock>(block);
                }
                else
                {
                    //todo IValueTaskSource
                    source = new TaskCompletionSource<ISendBlock>();
                    return new ValueTask<ISendBlock>(source.Task);
                }
            }
        }

        public bool Advance(ISendBlock sendBlock)
        {
            if (sendQueue.TryDequeue(out var first))
            {
                if (first != sendBlock)
                {
                    throw new InvalidOperationException($"发送队列出现排序错误");
                }
                sendBlock.SendSuccess();
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
