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
        internal protected class Writer : IBufferWriter<byte>, IWriter, ISendBlock
        {
            private TcpSendPipe sendPipe;
            private byte[] buffer;
            /// <summary>
            /// 当前游标位置
            /// </summary>
            int index = 4;

            public Writer(TcpSendPipe sendPipe)
            {
                this.sendPipe = sendPipe;
                Reset();
            }

            void Reset()
            {
                if (buffer == null)
                {
                    buffer = ArrayPool<byte>.Shared.Rent(512);
                }

                index = 4;
            }

            public void Advance(int count)
            {
                index += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                if (buffer.Length - index >= sizeHint)
                {
                    //现有数组足够长；
                    return new Memory<byte>(buffer, index, sizeHint);
                }
                else
                {
                    return default;
                }
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                if (buffer.Length - index >= sizeHint)
                {
                    //现有数组足够长；
                    return new Span<byte>(buffer, index, sizeHint);
                }
                else
                {
                    return default;
                }
            }

            public void Discard()
            {
                Reset();
            }

            public void PackSuccess()
            {
                buffer.AsSpan().Write(index);
                sendPipe.Push2Queue(this);
            }

            public void SendSuccess()
            {
                Reset();
            }

            public ReadOnlyMemory<byte> SendMemory => new ReadOnlyMemory<byte>(buffer, 0, index);

            public void NeedToResend()
            {
                throw new NotImplementedException();
            }
        }

        ConcurrentQueue<Writer> sendQueue = new ConcurrentQueue<Writer>();

        /// <summary>
        /// 发送失败队列
        /// </summary>
        ConcurrentQueue<Writer> sendFailQueue = new ConcurrentQueue<Writer>();
        private void Push2Queue(Writer writer)
        {
            if (source != null)//多线程问题 todo lock
            {
                source.SetResult(writer);
                source = null;
            }
            sendQueue.Enqueue(writer);
        }

        /// <summary>
        /// 取得一个全新写入器
        /// </summary>
        /// <returns></returns>
        internal IWriter GetNewwriter()
        {
            return new Writer(this);
        }

        TaskCompletionSource<ISendBlock> source;
        /// <summary>
        /// 取得下一个待发送消息。
        /// </summary>
        /// <returns></returns>
        public ValueTask<ISendBlock> PeekNext()
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
                source = new TaskCompletionSource<ISendBlock>();
                return new ValueTask<ISendBlock>(source.Task);
            }
        }
    }
}
