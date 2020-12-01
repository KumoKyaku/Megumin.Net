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
using System.Threading.Tasks.Sources;

namespace Megumin.Remote
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>消息报头结构：
    /// Lenght(总长度，包含自身报头) [int] [4] + RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]</remarks>
    public partial class TcpRemote : RpcRemote, IRemote, IRemoteUID<int>
    {
        public int ID { get; } = InterlockedID<IRemoteID>.NewID();
        public virtual int UID { get; set; }
        public bool IsVaild => RemoteState == WorkState.Working;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public Socket Client { get; protected set; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;

        public enum WorkState
        {
            /// <summary>
            /// 所有工作停止，不允许Push到发送队列。
            /// </summary>
            Stoped = -4,

            /// <summary>
            /// 正在停止,不允许Push到发送队列，底层停止发送。
            /// </summary>
            StopingAll = -3,
            /// <summary>
            /// 正在停止,不允许Push到发送队列，但底层仍可能正在发送。
            /// </summary>
            StopingWaitQueueSending = -2,
            /// <summary>
            /// 从未尝试开始
            /// </summary>
            NotStart = -1,
            /// <summary>
            /// 发送接收都正常
            /// </summary>
            Working = 0,
        }
        
        /// <summary>
        /// 当前状态,使用此标记控制 底层发送 底层接收 接收数据处理三个循环正确退出。
        /// </summary>
        public WorkState RemoteState { get;internal protected set; } = WorkState.NotStart;

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
            disconnector.tcpRemote = this;
            if (Client.Connected)
            {
                //服务器接受设置Socket
            }
        }

        /// <summary>
        /// 开始发送接收
        /// </summary>
        internal protected virtual void StartWork()
        {
            if (RemoteState == WorkState.NotStart)
            {
                RemoteState = WorkState.Working;
                FillRecvPipe(pipe.Writer);
                StartReadRecvPipe(pipe.Reader);
                ReadSendPipe(SendPipe);
            }
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
                    await Client.ConnectAsync(endPoint).ConfigureAwait(false);
                    IsConnecting = false;
                    disconnector.tcpRemote = this;
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
            disconnector?.Disconnect(triggerOnDisConnect, waitSendQueue);
        }
    }

    public partial class TcpRemote : ISendable, ISendCanAwaitable
    {
        protected override void Send(int rpcID, object message, object options = null)
        {
            //todo 检查当前是否允许发送，可能已经处于断开阶段，不在允许新消息进入发送缓存区
            var allowSend = RemoteState == WorkState.Working || RemoteState == WorkState.NotStart;
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

        /// <summary>
        /// 发送管道
        /// </summary>
        /// <remarks>发送管道没有涵盖所有案例，尽量不要给外界访问</remarks>
        protected TcpSendPipe SendPipe { get; } = new TcpSendPipe();

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

                    if (RemoteState != WorkState.Working 
                        && RemoteState != WorkState.StopingWaitQueueSending)
                    {
                        return;
                    }

                    IsSending = true;
                }

                try
                {
                    var target = await sendPipe.ReadNext();

                    if (RemoteState != WorkState.Working
                        && RemoteState != WorkState.StopingWaitQueueSending)
                    {
                        //拿到待发送数据时，Socket已经不能发送了
                        target.NeedToResend();
                        return;
                    }

#if NETSTANDARD2_1
                var length = target.SendMemory.Length;
                var result = await Client.SendAsync(target.SendMemory, SocketFlags.None);
#else
                    var length = target.SendSegment.Count;
                    var result = await Client.SendAsync(target.SendSegment, SocketFlags.None).ConfigureAwait(false);
#endif

                    if (result == length)
                    {
                        //发送成功
                        target.SendSuccess();
                    }
                    else
                    {
                        target.NeedToResend();
                        //发送不成功，result 是错误码
                        //https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.sockettaskextensions.sendasync?view=netstandard-2.0
                        disconnector?.OnSendError((SocketError)result);
                        //todo 如果错误码和要发送的字节恰好相等怎么办？
                    }

                    IsSending = false;
                }
                catch (SocketException e)
                {
                    disconnector?.OnSendError((SocketError)e.ErrorCode);
                    IsSending = false;
                    return;
                }
            }
        }
    }

    public partial class TcpRemote : IReceiveMessage
    {
        /// <summary>
        /// 不使用线程同步上下文，全部推送到线程池调用。useSynchronizationContext 用来保证await前后线程一致。
        /// </summary>
        /// <remarks>
        /// <para/>useSynchronizationContext 如果为true的话，
        /// <para/>那么pipe read write 异步后续只会在调用线程执行。
        /// <para/>构造 连接 StartWork调用链通常导致pipe异步后续在unity中会被锁定在主线程。
        /// <para/>https://source.dot.net/#System.IO.Pipelines/System/IO/Pipelines/PipeAwaitable.cs,115
        /// </remarks>
        public Pipe pipe { get; } = new Pipe(new PipeOptions(useSynchronizationContext: false));

        /// <summary>
        /// 当前socket是不是在接收。
        /// </summary>
        public bool IsReceiving { get; protected set; }
        
        /// <summary>
        /// 从Socket接收
        /// </summary>
        /// <param name="pipeWriter"></param>
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

                    if (RemoteState != WorkState.Working)
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
                    count = await Client.ReceiveAsync(segment, SocketFlags.None).ConfigureAwait(false);
#endif

                    if (count == 0)
                    {
                        disconnector?.OnRecv0();
                    }
                    else
                    {
                        pipeWriter.Advance(count);
                        _ = pipeWriter.FlushAsync();
                    }
                    
                    IsReceiving = false;
                }
                catch (SocketException e)
                {
                    disconnector?.OnRecvError((SocketError)e.ErrorCode);
                    IsReceiving = false;
                    return;
                }
            }
        }

        /// <summary>
        /// 正在处理消息
        /// </summary>
        public bool IsDealReceiving { get; protected set; }
        /// <summary>
        /// 开始读取接收到的数据
        /// </summary>
        /// <param name="pipeReader"></param>
        protected async void StartReadRecvPipe(PipeReader pipeReader)
        {
            while (true)
            {
                lock (pipeReader)//如果不使用IsDealReceiving 标记直接在lock中处理，第二个调用者会卡住。
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
                long unReadLenght = unDealBuffer.Length;
                int offset = 0;

                try
                {
                    //处理粘包
                    while (unReadLenght > 4)
                    {
                        //下一个包体总长度
                        var nextSegmentLength = unDealBuffer.ReadInt();

                        if (unReadLenght >= nextSegmentLength)
                        {
                            //取得消息体
                            var body = unDealBuffer.Slice(offset + 4, nextSegmentLength - 4);
                            
                            //先计数后处理，如果某个数据段出现错误可以略过该段
                            unReadLenght -= nextSegmentLength;
                            offset += nextSegmentLength;
                            ProcessBody(body);
                        }
                        else
                        {
                            //半包，继续读取
                            if (nextSegmentLength > 1024 * 256)
                            {
                                //todo，长度非常大可能是一个错误.
                            }
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger?.Log(e.ToString());
                }

                //标记已使用数据，要先使用在标记，不然数据可能就被释放了
                var pos = result.Buffer.GetPosition(offset);
                pipeReader.AdvanceTo(pos);

                IsDealReceiving = false;

                if (result.IsCompleted || result.IsCanceled)
                {
                    //pipeReader.AdvanceTo(result.Buffer.End);
                    return;
                }
            }
        }


        /// <remarks>留给Unity用的。在unity中赋值</remarks>
        public float LastReceiveTimeFloat { get; protected set; } = float.MaxValue;
    }
}
