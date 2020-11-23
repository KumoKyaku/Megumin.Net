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
        /// 当前消息需要重写发送
        /// </summary>
        void NeedToResend();
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
        /// 消息打包成功
        /// </summary>
        void PackSuccess();
    }

    /// <summary>
    /// Tcp发送管道 存在并发/异步函数重入问题
    /// </summary>
    public class TcpSendPipe
    {
        public int DefaultWriterSize { get; set; } = 8192;
        internal protected class Writer : IBufferWriter<byte>, IWriter, ISendBlock
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
                    if (buffer == null)
                    {
                        buffer = ArrayPool<byte>.Shared.Rent(sendPipe.DefaultWriterSize);
                    }

                    index = 4;
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
                    return new Memory<byte>(buffer, index, sizeHint);
                }
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                lock (syncLock)
                {
                    Ensure(sizeHint);
                    return new Span<byte>(buffer, index, sizeHint);
                }
            }

            /// <summary>
            /// 确保当前buffer足够大
            /// </summary>
            /// <param name="sizeHint"></param>
            void Ensure(int sizeHint)
            {
                var leftLength = buffer.Length - index;
                if (leftLength >= sizeHint)
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

            public void PackSuccess()
            {
                buffer.AsSpan().Write(index);//在起始位置写入长度
                sendPipe.Push2Queue(this);
            }

            public void SendSuccess()
            {
                Reset();
            }

            public ReadOnlyMemory<byte> SendMemory => new ReadOnlyMemory<byte>(buffer, 0, index);

            public void NeedToResend()
            {
                sendPipe.Push2Queue(this);
            }

            public ArraySegment<byte> SendSegment => new ArraySegment<byte>(buffer, 0, index);
        }

        ConcurrentQueue<Writer> sendQueue = new ConcurrentQueue<Writer>();

        /// <summary>
        /// 发送失败队列
        /// </summary>
        ConcurrentQueue<Writer> sendFailQueue = new ConcurrentQueue<Writer>();

        protected readonly object _pushLock = new object();
        private void Push2Queue(Writer writer)
        {
            TaskCompletionSource<ISendBlock> curSource = null;
            lock (_pushLock)
            {
                if (source != null)
                {
                    curSource = source;
                    source = null;
                }
                else
                {
                    sendQueue.Enqueue(writer);
                }
            }

            curSource?.SetResult(writer);
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
            if (sendFailQueue.TryDequeue(out var writer))
            {
                return new ValueTask<ISendBlock>(writer);
            }
            else if (sendQueue.TryDequeue(out var send))
            {
                return new ValueTask<ISendBlock>(send);
            }
            else if (source != null)
            {
                throw new Exception(); //todo 已经有个发送任务在等了。
            }
            else
            {
                //todo IValueTaskSource
                source = new TaskCompletionSource<ISendBlock>();
                return new ValueTask<ISendBlock>(source.Task);
            }
        }
    }
}
