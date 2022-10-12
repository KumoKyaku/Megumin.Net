using Megumin.Message;
using Net.Remote;
using System;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
    /// <para/> 
    /// Q：异步方法会不会延长声明周期，导致对象永不销毁？
    /// A：存疑，感觉应该不会，需要测试。异步调用会注册到IOCP线程池中。如果异步接收没收到0字节或者异常，那么对象会一直活着。
    /// </remarks>
    public abstract class RemoteBase : ISendable
    {
        /// <summary>
        /// 究竟要不要初始化内嵌消息.
        /// </summary>
        static RemoteBase()
        {
            //MessageLUT.Regist(Heartbeat.Default);
        }

        /// <summary>
        /// 记录器
        /// </summary>
        [Obsolete("Use TraceListener instead.", true)]
        public IMeguminRemoteLogger Logger { get; set; }
        public System.Diagnostics.TraceListener TraceListener { get; set; }

        /// <summary>
        /// 当网络连接已经断开, 发送和接受可能有一个没有完全停止。
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// <param name="error"></param>
        /// <param name="activeOrPassive">主动断开还是被动断开</param>
        /// <remarks>主要用于通知外部停止继续发送</remarks>
        protected virtual void PreDisconnect(
            SocketError error = SocketError.SocketError,
            ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {

        }

        /// <summary>
        /// 断开连接之后
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="activeOrPassive">主动断开还是被动断开</param>
        /// <remarks>可以用于触发重连，并将现有发送缓冲区转移到心得连接中</remarks>
        protected virtual void OnDisconnect(
            SocketError error = SocketError.SocketError,
            ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {

        }

        /// <summary>
        /// 断开连接之后
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="activeOrPassive">主动断开还是被动断开</param>
        protected virtual void PostDisconnect(
           SocketError error = SocketError.SocketError,
           ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {

        }

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
            IMeguminFormater formater;
            ///优先使用MessageLut，因为MessageLut是主动注册的。
            if (!MessageLUT.TryGetFormater(message.GetType(), out formater))
            {
                ///对象自身就时序列化器
                formater = message as IMeguminFormater;
            }

            if (formater != null)
            {
                WriteRpcIDCMD(writer, rpcID, options);

                //写入MessageID
                var span = writer.GetSpan(4);
                span.Write(formater.MessageID);
                writer.Advance(4);

                try
                {
                    formater.Serialize(writer, message, options);
                }
                catch (Exception e)
                {
                    TraceListener?.WriteLine($"序列化过程出现异常。Message:{message}。\n {e}");
                    return false;
                }

                return true;
            }
            else
            {
                TraceListener?.WriteLine($"没有找到Formater。Message:{message}。");
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
                WriteRpcIDCMD(writer, rpcID, options);

                foreach (var item in sequence)
                {
                    writer.Write(item.Span);
                }

                return true;
            }
            catch (Exception e)
            {
                TraceListener?.WriteLine($"转发用序列化过程出现异常。Lenght:{sequence.Length}。\n {e}");
                return false;
            }
        }

        /// <summary>
        /// 写入rpcID CMD
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="options"></param>
        protected virtual void WriteRpcIDCMD(IBufferWriter<byte> writer, int rpcID, object options = null)
        {
            //写入rpcID CMD
            var span = writer.GetSpan(6);
            span.Write(rpcID);
            short cmd = 0;
            if (options is ICmdOption cmdOption)
            {
                cmd = cmdOption.Cmd;
            }
            span.Slice(4).Write(cmd);
            writer.Advance(6);
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
                TraceListener?.WriteLine($"反序列化过程出现异常。MessageID:{messageID}--Lenght:{byteSequence.Length}。\n {e}");
                message = default;
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
            (int messageID, in ReadOnlyMemory<byte> byteSequence,
            out object message, object options = null)
        {
            try
            {
                message = MessageLUT.Deserialize(messageID, byteSequence, options);
                return true;
            }
            catch (Exception e)
            {
                TraceListener?.WriteLine($"反序列化过程出现异常。MessageID:{messageID}--Lenght:{byteSequence.Length}。\n {e}");
                message = default;
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
            (int messageID, in ReadOnlySpan<byte> byteSequence,
            out object message, object options = null)
        {
            try
            {
                message = MessageLUT.Deserialize(messageID, byteSequence, options);
                return true;
            }
            catch (Exception e)
            {
                TraceListener?.WriteLine($"反序列化过程出现异常。MessageID:{messageID}--Lenght:{byteSequence.Length}。\n {e}");
                message = default;
                return false;
            }
        }

        /// <summary>
        /// 发送rpcID和消息
        /// </summary>
        public abstract void Send(int rpcID, object message, object options = null);

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
        /// <remarks>在Unity中也可以重写这个函数，判断调用线程是不是unity主线程，如果是则不需要转化线程</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool UseThreadSchedule(int rpcID, short cmd, int messageID, object message)
        {
            if (message is IReceiveThreadControlable controlable && controlable.ReceiveThreadPost2ThreadScheduler.HasValue)
            {
                return controlable.ReceiveThreadPost2ThreadScheduler.Value;
            }
            return Post2ThreadScheduler;
        }

        /// <summary>
        /// 返回一个空对象，在没有返回时使用。同步完成。
        /// </summary>
        internal protected static readonly ValueTask<object> NullResult
            = new ValueTask<object>(result: null);

        /// <summary>
        /// 开始时想通过MSGID，固定[256-512)消息id时，自动Echo 此消息。
        /// 结果发现会造成两端死循环，两边不停的重复发送这条消息，造成网络风暴,此方式行不通。
        /// <para></para>
        /// 改为由CMD == 1和<seealso cref="IPreReceiveable.PreReceiveType"/> == 1实现。
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <param name="stopReceive"></param>
        /// <returns></returns>
        protected virtual ValueTask<object> PreReceive(short cmd, int messageID, object message, out bool stopReceive)
        {
            stopReceive = true;

            if ((cmd & 0b0000_0001) != 0)
            {
                //Echo
                return new ValueTask<object>(result: message);
            }

            if (message is IAutoResponseable autoRespones)
            {
                if (autoRespones.PreReceiveType == 2)
                {
                    return autoRespones.GetResponse(message);
                }
            }

            if (message is IPreReceiveable receive)
            {
                if (receive.PreReceiveType == 1)
                {
                    //Echo
                    return new ValueTask<object>(result: message);
                }
            }

            stopReceive = false;
            return NullResult;
        }

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
        /// <para/> 既然不能标准化，所以也不能声明委托事件，会导致控制流更加复杂。
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
        /// <param name="options"></param>
        protected abstract void DeserializeSuccess(int rpcID, short cmd, int messageID, object message, object options = null);

        /// <summary>
        /// 处理一个完整的消息包，未解析报头
        /// </summary>
        /// <remarks>
        /// 如果想要实现反序列化前转发，重写此方法。
        /// </remarks>
        protected virtual void ProcessBody(in ReadOnlySequence<byte> byteSequence,
                                           object options = null)
        {
            //读取RpcID 和 消息ID
            var (RpcID, CMD, MessageID) = byteSequence.ReadHeader();
            ProcessBody(byteSequence.Slice(10), RpcID, CMD, MessageID, options);
        }

        protected virtual void ProcessBody(in ReadOnlySpan<byte> byteSequence,
                                           object options = null)
        {
            //读取RpcID 和 消息ID
            var (RpcID, CMD, MessageID) = byteSequence.ReadHeader();
            ProcessBody(byteSequence.Slice(10), RpcID, CMD, MessageID, options);
        }

        protected virtual void ProcessBody(in ReadOnlyMemory<byte> byteSequence,
                                           object options = null)
        {
            //读取RpcID 和 消息ID
            var (RpcID, CMD, MessageID) = byteSequence.ReadHeader();
            ProcessBody(byteSequence.Slice(10), RpcID, CMD, MessageID, options);
        }

        /// <summary>
        /// 反序列化失败时，是否将直接字节数组传递到上层。
        /// </summary>
        public bool UseByteArrayOnDeserializeError = true;
        /// <summary>
        /// 处理一个完整的消息包，已分离报头
        /// </summary>
        protected virtual void ProcessBody(in ReadOnlySequence<byte> bodyBytes,
                                           int RpcID,
                                           short CMD,
                                           int MessageID,
                                           object options = null)
        {
            if (TryDeserialize(MessageID, bodyBytes, out var message, options))
            {
                DeserializeSuccess(RpcID, CMD, MessageID, message, options);
            }
            else
            {
                if (UseByteArrayOnDeserializeError)
                {
                    //反序列化失败,返回上层一个byte[] 
                    byte[] bytes = new byte[bodyBytes.Length];
                    bodyBytes.CopyTo(bytes);
                    DeserializeSuccess(RpcID, CMD, MessageID, bytes, options);
                }
            }
        }

        /// <summary>
        /// 处理一个完整的消息包，已分离报头
        /// </summary>
        protected virtual void ProcessBody(in ReadOnlySpan<byte> bodyBytes,
                                           int RpcID,
                                           short CMD,
                                           int MessageID,
                                           object options = null)
        {
            if (TryDeserialize(MessageID, bodyBytes, out var message, options))
            {
                DeserializeSuccess(RpcID, CMD, MessageID, message, options);
            }
            else
            {
                if (UseByteArrayOnDeserializeError)
                {
                    //反序列化失败,返回上层一个byte[] 
                    byte[] bytes = new byte[bodyBytes.Length];
                    bodyBytes.CopyTo(bytes);
                    DeserializeSuccess(RpcID, CMD, MessageID, bytes, options);
                }
            }
        }

        /// <summary>
        /// 处理一个完整的消息包，已分离报头
        /// </summary>
        protected virtual void ProcessBody(in ReadOnlyMemory<byte> bodyBytes,
                                           int RpcID,
                                           short CMD,
                                           int MessageID,
                                           object options = null)
        {
            if (TryDeserialize(MessageID, bodyBytes, out var message, options))
            {
                DeserializeSuccess(RpcID, CMD, MessageID, message, options);
            }
            else
            {
                if (UseByteArrayOnDeserializeError)
                {
                    //反序列化失败,返回上层一个byte[] 
                    byte[] bytes = new byte[bodyBytes.Length];
                    bodyBytes.CopyTo(bytes);
                    DeserializeSuccess(RpcID, CMD, MessageID, bytes, options);
                }
            }
        }
    }
}
