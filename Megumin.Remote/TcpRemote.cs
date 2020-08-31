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
    public partial class TcpRemote : RpcRemote, IRemote, IRemoteUID<int>
    {
        public int ID { get; } = InterlockedID<IRemote>.NewID();
        public virtual int UID { get; set; }
        public bool IsVaild { get; protected set; } = true;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public Socket Client { get; protected set; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;
        /// <summary>
        /// 当前工作状态0：发送接收都正常； 1:从未尝试开始； 2：手动终止； 3：远端断开连接；
        /// </summary>
        public int WorkState { get; protected set; } = 1;

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
        internal protected virtual void SetSocket(Socket socket, bool reconnectForce = false)
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
        /// 开始发送接收
        /// </summary>
        internal protected virtual void StartWork()
        {
            WorkState = 0;
            FillRecvPipe(pipe.Writer);
            ReadRecePipe(pipe.Reader);
            ReadSendPipe(SendPipe);
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
                    WaitSocketError();//注册断开流程
                    StartWork();
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

        public virtual Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
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
            //todo 进入断开流程，不允许外部继续Send


            if (waitSendQueue)
            {
                //todo 等待当前发送缓冲区发送结束。
            }

            //进入清理阶段
            StopWork();

            if (triggerOnDisConnect)
            {
                //触发回调
                OnDisconnect(SocketError.SocketError, ActiveOrPassive.Active);
                PostDisconnect(SocketError.SocketError, ActiveOrPassive.Active);
            }

            IsVaild = false;
        }

        /// <summary>
        /// 开始被动断开流程
        /// </summary>
        async void WaitSocketError()
        {
            var errorCode = await OnSocketError.Task;
            //socket已经发生了错误。

            //进入清理阶段
            StopWork();

            //触发回调
            OnDisconnect(errorCode);
            PostDisconnect(errorCode);
            IsVaild = false;
        }

        /// <summary>
        /// 停止接收工作
        /// </summary>
        void StopWork()
        {
            //关闭接收，这个过程中可能调用本身出现异常。
            //也可能导致异步接收部分抛出，由于disconnectSignal只能使用一次，所有这个阶段异常都会被忽略。
            try
            {
                Client.Shutdown(SocketShutdown.Both);
                Client.Disconnect(false);
            }
            finally
            {
                Client.Close();
                Client.Dispose();
            }
        }

        /// <summary>
        /// 用一个handle来保证一个socket只触发一次断开连接。
        /// 触发断开，清理发送接收，调用OnDisconnect 和 PostDisconnect
        /// </summary>
        readonly TaskCompletionSource<SocketError> OnSocketError = new TaskCompletionSource<SocketError>();

        protected override void OnDisconnect(
            SocketError error = SocketError.SocketError,
            ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {

        }

        protected override void PostDisconnect(
            SocketError error = SocketError.SocketError,
            ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {

        }
    }

    public partial class TcpRemote : ISendable, ISendCanAwaitable
    {
        public bool IsSending { get; protected set; }

        /// <summary>
        /// 开始读取发送管道，使用Socket发送消息
        /// </summary>
        public async void ReadSendPipe(TcpSendPipe sendPipe)
        {
            while (true)
            {
                lock (sendPipe)
                {
                    if (IsSending)
                    {
                        return;
                    }
                    IsSending = true;
                }

                var target = await sendPipe.ReadNext();

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

                IsSending = false;
            }
        }

        /// <summary>
        /// 发送管道
        /// </summary>
        /// <remarks>发送管道没有涵盖所有案例，尽量不要给外界访问</remarks>
        protected TcpSendPipe SendPipe { get; } = new TcpSendPipe();

        protected virtual void Send(int rpcID, object message, object options = null)
        {
            //todo 检查当前是否允许发送，可能已经处于断开阶段，不在允许新消息进入发送缓存区
            var allowSend = true;
            if (!allowSend)
            {
                if (rpcID > 0)
                {
                    //对于已经注册了Rpc的消息,直接触发异常。
                    RpcCallbackPool.TrySetException(rpcID * -1, new SocketException(-1));
                }
                else
                {
                    throw new SocketException(-1);
                }
            }

            var writer = SendPipe.GetWriter();
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

            StartWork();
        }

        public void Send(object message, object options = null)
        {
            Send(0, message, options);
        }

        public IMiniAwaitable<(RpcResult result, Exception exception)>
            Send<RpcResult>(object message, object options = null)
        {
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

        public virtual IMiniAwaitable<RpcResult> SendSafeAwait<RpcResult>
            (object message, Action<Exception> OnException = null, object options = null)
        {
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

    public partial class TcpRemote : IReceiveMessage
    {
        public Pipe pipe { get; } = new Pipe();

        /// <summary>
        /// 当前socket是不是在接收。
        /// </summary>
        public bool IsReceiving { get; protected set; }
        protected virtual async void FillRecvPipe(PipeWriter pipeWriter)
        {
            while (true)
            {
                lock (pipeWriter)
                {
                    if (IsReceiving)
                    {
                        return;
                    }
                    IsReceiving = true;
                }

                int queryCount = 8192;
                var buffer = pipeWriter.GetMemory(queryCount);
                int count = 0;

                try
                {

#if NETSTANDARD2_1
                    count = await Client.ReceiveAsync(buffer, SocketFlags.None);
#else

                    if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
                    {
                        //重设长度
                        segment = new ArraySegment<byte>(segment.Array, segment.Offset, buffer.Length);
                    }
                    else
                    {
                        //无法获取数组片段。
                        throw new NotSupportedException($"buffer 无法转化为数组。");
                    }
                    count = await Client.ReceiveAsync(segment, SocketFlags.None);
#endif

                    if (count == 0)
                    {
                        //收到0字节 表示远程主动断开连接
                        this.ToString();//debug
                    }
                }
                catch (SocketException e)
                {
                    pipeWriter.Complete();
                    WorkState = 3;
                    return;
                }

                pipeWriter.Advance(count);
                _ = pipeWriter.FlushAsync();
                IsReceiving = false;
            }
        }

        /// <summary>
        /// 正在处理消息
        /// </summary>
        public bool IsDealReceiving { get; protected set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pipeReader"></param>
        protected async void ReadRecePipe(PipeReader pipeReader)
        {
            while (true)
            {
                lock (pipeReader)
                {
                    if (IsDealReceiving)
                    {
                        return;
                    }
                    IsDealReceiving = true;
                }
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

                if (result.IsCompleted || result.IsCanceled)
                {
                    throw new Exception("这里处理终止");
                }
                IsDealReceiving = false;
            }
        }

        /// <summary>
        /// 回复给远端
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="replyMessage"></param>
        protected override void Reply(int rpcID, object replyMessage)
        {
            Send(rpcID, replyMessage);
        }

        public float LastReceiveTimeFloat { get; } = float.MaxValue;
    }
}
