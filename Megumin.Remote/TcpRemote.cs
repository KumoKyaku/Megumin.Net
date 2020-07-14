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
    /// 
    /// </summary>
    /// <remarks>消息报头结构：
    /// Lenght(总长度，包含自身报头) [int] [4] + RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]</remarks>
    public partial class TcpRemote:IRemote
    {
        public int ID { get; } = InterlockedID<IRemote>.NewID();
        /// <summary>
        /// 这是留给用户赋值的
        /// </summary>
        public virtual int UID { get; set; }
        public bool IsVaild { get; protected set; } = true;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public RpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);

        public Socket Client { get; protected set; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;


        /// <summary>
        /// Mono/IL2CPP 请使用中使用<see cref="TcpRemote(AddressFamily)"/>
        /// </summary>
        public TcpRemote()
        {

        }

        /// <remarks>
        /// <para>SocketException: Protocol option not supported</para>
        /// http://www.schrankmonster.de/2006/04/26/system-net-sockets-socketexception-protocol-not-supported/
        /// </remarks>
        public TcpRemote(AddressFamily addressFamily)
        {
            SetSocket(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp));
        }

        /// <summary>
        /// 设置Client Socket
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="reconnectForce"></param>
        public virtual void SetSocket(Socket socket,bool reconnectForce = false)
        {
            if (Client != null)
            {
                throw new InvalidOperationException("当前已经有Socket了，不允许重设");
            }

            this.Client = socket;
            if (Client.Connected)
            {
                //服务器接受设置Socket
            }
            IsVaild = true;
        }

        /// <summary>
        /// 认证阶段
        /// </summary>
        protected virtual ValueTask<int> Auth()
        {
            return new ValueTask<int>(0);
        }

        /// <summary>
        /// 开始工作
        /// </summary>
        public virtual void WorkStart()
        {
            ReceiveStart();
            SendStart();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public partial class TcpRemote : IConnectable
    {
        /// <summary>
        /// 连接保护器，防止多次调用
        /// </summary>
        readonly object _connectlock = new object();
        /// <summary>
        /// 正在连接
        /// </summary>
        bool IsConnecting = false;
        private async Task ConnectAsync(Socket socket, IPEndPoint endPoint, int retryCount = 0)
        {
            lock (_connectlock)
            {
                if (IsConnecting)
                {
                    throw new InvalidOperationException("连接正在进行中");
                }
                IsConnecting = true;
            }

            if (socket.Connected)
            {
                throw new ArgumentException("socket已经连接");
            }

            while (retryCount >= 0)
            {
                try
                {
                    await Client.ConnectAsync(endPoint);
                    IsConnecting = false;
                    return;
                }
                catch (Exception)
                {
                    if (retryCount <= 0)
                    {
                        IsConnecting = false;
                        throw;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }
        }

        public Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            ConnectIPEndPoint = endPoint;
            if (Client == null)
            {
                SetSocket(new Socket(SocketType.Stream, ProtocolType.Tcp));
            }
            return ConnectAsync(Client, endPoint, retryCount);
        }

        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 断开连接之后
        /// </summary>
        protected virtual void PostDisconnect()
        {

        }
    }

    public partial class TcpRemote : ISendable, ISendCanAwaitable
    {
        /// <summary>
        /// 开始发送消息
        /// </summary>
        public async void SendStart()
        {
            var target = await SendPipe.PeekNext();

#if NETSTANDARD2_1
            var length = target.SendMemory.Length;
            var result = await Client.SendAsync(target.SendMemory, SocketFlags.None);
#else
            var length = target.SendSegment.Count;
            var result = await Client.SendAsync(target.SendSegment, SocketFlags.None);
#endif

            if (result == length)
            {
                //dequeue?
                //成功？
                target.SendSuccess();
            }

            //todo 发送失败。
            SendStart();
        }

        /// <summary>
        /// 发送管道
        /// </summary>
        /// <remarks>发送管道没有涵盖所有案例，尽量不要给外界访问</remarks>
        protected TcpSendPipe SendPipe { get; } = new TcpSendPipe();

        protected virtual void Send(int rpcID, object message, object options = null)
        {
            var writer = SendPipe.GetNewwriter();
            if (TrySerialize(writer, rpcID, message, options))
            {
                //序列化成功
                writer.PackSuccess();
            }
            else
            {
                //序列化失败
                writer.Discard();
            }

            SendStart();
        }

        /// <summary>
        /// 序列化消息
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <returns></returns>
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
            catch (Exception)
            {
                //todo log;
                return false;
            }
        }

        /// <summary>
        /// 序列化消息
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="sequence"></param>
        /// <param name="options"></param>
        /// <returns></returns>
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
            catch (Exception)
            {
                //todo log;
                return false;
            }
        }

        public void Send(object message, object options = null)
        {
            Send(0, message, options);
        }

        public IMiniAwaitable<(RpcResult result, Exception exception)>
            Send<RpcResult>(object message, object options = null)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(options);

            try
            {
                Send(rpcID, message);
                return source;
            }
            catch (Exception e)
            {
                RpcCallbackPool.TrySetException(rpcID * -1, e);
                return source;
            }
        }

        public IMiniAwaitable<RpcResult> SendSafeAwait<RpcResult>
            (object message, Action<Exception> OnException = null, object options = null)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(OnException, options);

            try
            {
                Send(rpcID, message);
                return source;
            }
            catch (Exception e)
            {
                source.CancelWithNotExceptionAndContinuation();
                OnException?.Invoke(e);
                return source;
            }
        }
    }

    public partial class TcpRemote : IReceiveMessage, IObjectMessageReceiver
    {
        public Pipe pipe { get; } = new Pipe();
        /// <summary>
        /// 开始接收消息；
        /// </summary>
        private void ReceiveStart()
        {
            FillPipe(pipe.Writer);
            ReadPipe(pipe.Reader);
        }

        private async void FillPipe(PipeWriter pipeWriter)
        {
            int queryCount = 8192;
            var buffer = pipeWriter.GetMemory(queryCount);

#if NETSTANDARD2_1
            var count = await Client.ReceiveAsync(buffer, SocketFlags.None);
#else
            int count = 0;
            if (MemoryMarshal.TryGetArray<byte>(buffer,out var segment))
            {
                //重设长度
                segment = new ArraySegment<byte>(segment.Array,segment.Offset,buffer.Length);
                count = await Client.ReceiveAsync(segment, SocketFlags.None);
            }
            else
            {
                //todo log
            }
#endif

            pipeWriter.Advance(count);
            _ = pipeWriter.FlushAsync();
            FillPipe(pipeWriter);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pipeReader"></param>
        protected async void ReadPipe(PipeReader pipeReader)
        {
            var result = await pipeReader.ReadAsync();
        
            //剩余未处理消息buffer
            var unDealBuffer = result.Buffer;

            while (unDealBuffer.Length > 4)
            {
                //包体总长度
                var length = unDealBuffer.ReadInt();
                if (unDealBuffer.Length >= length)
                {
                    ///取得消息体
                    var body = unDealBuffer.Slice(4, length - 4);

                    ProcessBody(body, null);
                    //标记已使用数据
                    var pos = result.Buffer.GetPosition(length);
                    pipeReader.AdvanceTo(pos);

                    unDealBuffer = unDealBuffer.Slice(length);//切除已使用部分
                }
                else
                {
                    break;
                }
            }

            //继续处理
            ReadPipe(pipeReader);
        }

        protected virtual bool TryDeserialize
            (int messageID, in ReadOnlySequence<byte> byteSequence,
            out object message, object options = null)
        {
            try
            {
                message = MessageLUT.Deserialize(messageID, byteSequence, options);
                return true;
            }
            catch (Exception)
            {
                //log todo
                message = default;
                return false;
            }
        }

        /// <summary>
        /// 处理一个完整的消息包
        /// </summary>
        protected virtual void ProcessBody
            (in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            //读取RpcID 和 消息ID
            var (RpcID, CMD, MessageID) = byteSequence.ReadHeader();
            if (TryDeserialize(MessageID, byteSequence.Slice(10), out var message, options))
            {
                DeserializeSuccess(RpcID, MessageID, message);
            }
            else
            {
                //todo 反序列化失败
            }
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
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool UseThreadSchedule(int rpcID, int messageID, object message)
        {
            return Post2ThreadScheduler;
        }

        /// <summary>
        /// 解析消息成功
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        protected async void DeserializeSuccess(int rpcID, int messageID, object message)
        {
            var post = true;//转换线程

            //消息处理程序的返回对象
            object reply = null;

            var trans = UseThreadSchedule(rpcID, messageID, message);
            if (trans)
            {
                reply = await MessageThreadTransducer.Push(rpcID, message, this);
            }
            else
            {
                reply = await DiversionProcess(rpcID, message);

                if (reply is Task<object> task)
                {
                    reply = await task;
                }

                if (reply is ValueTask<object> vtask)
                {
                    reply = await vtask;
                }
            }

            if (reply != null)
            {
                Reply(rpcID, reply);
            }
        }

        /// <summary>
        /// 回复给远端
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="replyMessage"></param>
        protected virtual void Reply(int rpcID, object replyMessage)
        {
            Send(rpcID * -1, replyMessage);
        }

        public float LastReceiveTimeFloat { get; } = float.MaxValue;

        ValueTask<object> IObjectMessageReceiver.Deal(int rpcID, object message)
        {
            return DiversionProcess(rpcID, message);
        }

        /// <summary>
        /// 分流普通消息和RPC回复消息
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual ValueTask<object> DiversionProcess(int rpcID,  object message)
        {
            if (rpcID < 0)
            {
                //这个消息是rpc返回（回复的RpcID为负数）
                RpcCallbackPool?.TrySetResult(rpcID, message);
                return new ValueTask<object>(result: null);
            }
            else
            {
                ///这个消息是非Rpc应答
                ///普通响应onRely
                return OnReceive(message);
            }
        }

        /// <summary>
        /// 返回一个空对象，在没有返回时使用。
        /// </summary>
        protected static readonly ValueTask<object> NullResult 
            = new ValueTask<object>(result: null);
        /// <summary>
        /// 通常用户在这里处理收到的消息
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <remarks>含有远程返回的rpc回复消息会被直接通过回调函数发送到异步调用处，不会触发这里</remarks>
        protected virtual ValueTask<object> OnReceive(object message)
        {
            return NullResult;
        }
    }
}
