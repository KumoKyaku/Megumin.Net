using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace Megumin.Remote
{
    /// <summary>
    /// 逻辑流程定义。
    /// 这个类用来定义Remote功能需要有哪些必要回调函数。
    /// 不定义在<see cref="IRemote"/>是因为这些函数都不应该是public的。
    /// 已经包含在<see cref="IRemote"/>中的函数，不再这里定义。
    /// <para></para>
    /// 通常这些函数都是业务逻辑需要用到的函数，并且与网络协议无关。
    /// 这个类起到一个必要实现函数列表备忘作用。
    /// <para></para>
    /// Lenght(总长度，包含自身报头) [int] [4] + RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]
    /// </summary>
    /// <remarks>为了防止冒泡效应，只定义抽象函数，声明极少数字段。
    /// <para></para>
    /// <see cref="TryDeserialize(int, in ReadOnlySequence{byte}, out object, object)"/>
    /// 没有设计成扩展函数或者静态函数是方便子类重写。
    /// </remarks>
    public abstract class RemoteBase : ISendable
    {
        /// <summary>
        /// 记录器
        /// </summary>
        public IMeguminRemoteLogger Logger { get; set; }

        /// <summary>
        /// 当网络连接已经断开
        /// </summary>
        /// <param name="error"></param>
        /// <param name="activeOrPassive">主动断开还是被动断开</param>
        /// <remarks>主要用于通知外部停止继续发送，在这个函数被动调用前，允许Send，在这个函数调用后，不在允许Send</remarks>
        protected abstract void OnDisconnect(
            SocketError error = SocketError.SocketError,
            ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive);

        /// <summary>
        /// 断开连接之后
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="activeOrPassive">主动断开还是被动断开</param>
        /// <remarks>可以用于触发重连，并将现有发送缓冲区转移到心得连接中</remarks>
        protected abstract void PostDisconnect(
            SocketError error = SocketError.SocketError,
            ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive);

        /// <summary>
        /// 序列化消息
        /// <para></para>
        /// Lenght(总长度，包含自身报头) [int] [4] 长度由writer外部自动封装，这里不用处理。
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>只处理 RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]</remarks>
        protected virtual bool TrySerialize(IBufferWriter<byte> writer, int rpcID, object message, object options = null)
        {
            try
            {
                //写入rpcID CMD
                var span = writer.GetSpan(10);
                span.Write(rpcID);
                span.Slice(4).Write((short)0); //CMD 为预留，填0
                writer.Advance(10);

                int messageID = MessageLUT.Serialize(writer, message, options);
                //补写消息ID到指定位置。 前面已经Advance了，这里不在Advance。
                span.Slice(6).Write(messageID);

                return true;
            }
            catch (Exception e)
            {
                Logger?.Log($"序列化过程出现异常。Message:{message}。\n {e}");
                return false;
            }
        }

        /// <summary>
        /// 序列化消息，转发用。转发是重写RPC 和 CMD，其他保持不动。
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="sequence">MessageID + 正文序列</param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks> RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]</remarks>
        protected virtual bool TrySerialize(IBufferWriter<byte> writer, int rpcID, in ReadOnlySequence<byte> sequence, object options = null)
        {
            try
            {
                //写入rpcID CMD
                var span = writer.GetSpan(6);
                span.Write(rpcID);
                span.Slice(4).Write((short)0); //CMD 为预留，填0
                writer.Advance(6);

                foreach (var item in sequence)
                {
                    writer.Write(item.Span);
                }

                return true;
            }
            catch (Exception e)
            {
                Logger?.Log($"转发用序列化过程出现异常。Lenght:{sequence.Length}。\n {e}");
                return false;
            }
        }

        /// <summary>
        /// 尝试反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="byteSequence"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>个别消息反序列化出现异常不能抛出，防止破环整个网络连接。</remarks>
        protected virtual bool TryDeserialize
            (int messageID, in ReadOnlySequence<byte> byteSequence,
            out object message, object options = null)
        {
            try
            {
                message = MessageLUT.Deserialize(messageID, byteSequence, options);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Log($"反序列化过程出现异常。MessageID:{messageID}--Lenght:{byteSequence.Length}。\n {e}");
                message = default;
                return false;
            }
        }

        /// <summary>
        /// 发送rpcID和消息
        /// </summary>
        protected abstract void Send(int rpcID, object message, object options = null);

        public void Send(object message, object options = null)
        {
            Send(0, message, options);
        }

        /// <summary>
        /// 默认关闭线程转换<see cref="MessageThreadTransducer.Update(double)"/>
        /// </summary>
        public bool Post2ThreadScheduler { get; set; } = false;

        /// <summary>
        /// 是否使用<see cref="MessageThreadTransducer"/>
        /// <para>精确控制各个消息是否切换到主线程。</para>
        /// <para>用于处理在某些时钟精确的且线程无关消息时跳过轮询等待。</para>
        /// 例如：同步两个远端时间戳的消息。
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool UseThreadSchedule(int rpcID, short cmd, int messageID, object message)
        {
            return Post2ThreadScheduler;
        }

        /// <summary>
        /// 返回一个空对象，在没有返回时使用。同步完成。
        /// </summary>
        internal protected static readonly ValueTask<object> NullResult
            = new ValueTask<object>(result: null);

        /// <summary>
        /// 通常用户在这里处理收到的消息
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <remarks>含有远程返回的rpc回复消息会被直接通过回调函数发送到异步调用处，不会触发这里
        /// <para/> 这个函数不要定义在<see cref="IReceiveMessage"/>里，
        /// 由于具体业务逻辑不同，这个函数的签名可能有很多中变化，不能标准化。
        /// </remarks>
        protected virtual ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return NullResult;
        }

        /// <summary>
        /// 解析消息成功
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        protected abstract void DeserializeSuccess(int rpcID, short cmd, int messageID, object message);

        /// <summary>
        /// 处理一个完整的消息包，未解析报头
        /// </summary>
        protected virtual void ProcessBody(in ReadOnlySequence<byte> byteSequence,
                                           object options = null)
        {
            //读取RpcID 和 消息ID
            var (RpcID, CMD, MessageID) = byteSequence.ReadHeader();
            ProcessBody(byteSequence.Slice(10), options, RpcID, CMD, MessageID);
        }

        /// <summary>
        /// 处理一个完整的消息包，已分离报头
        /// </summary>
        protected virtual void ProcessBody(in ReadOnlySequence<byte> byteSequence,
                                           object options,
                                           int RpcID,
                                           short CMD,
                                           int MessageID)
        {
            if (TryDeserialize(MessageID, byteSequence, out var message, options))
            {
                DeserializeSuccess(RpcID, CMD, MessageID, message);
            }
        }
    }
}
