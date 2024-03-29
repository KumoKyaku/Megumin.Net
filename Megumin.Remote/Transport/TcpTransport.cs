﻿using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Megumin.Message;
using Net.Remote;

namespace Megumin.Remote
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>消息报头结构：
    /// Lenght(总长度，包含自身报头) [int] [4] + RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]</remarks>
    public partial class TcpTransport : BaseTransport, ITransportable
    {
        public bool IsVaild => RemoteState == WorkState.Working;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public Socket Client { get; protected set; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;
        public EndPoint RemoteEndPoint => Client.RemoteEndPoint;
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
        public WorkState RemoteState { get; internal protected set; } = WorkState.NotStart;

        public TcpTransport()
        {

        }

        /// <remarks>
        /// 明确指定使用IPV4还是IPV6
        /// <para>SocketException: Protocol option not supported</para>
        /// http://www.schrankmonster.de/2006/04/26/system-net-sockets-socketexception-protocol-not-supported/
        /// </remarks>
        public TcpTransport(AddressFamily addressFamily)
        {
            SetSocket(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp));
        }

        /// <summary>
        /// 设置Client Socket
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="reconnectForce"></param>
        public virtual void SetSocket(Socket socket, bool reconnectForce = false)
        {
            if (Client != null)
            {
                throw new InvalidOperationException("当前已经有Socket了，不允许重设");
            }

            this.Client = socket;
            //每个socket都可以断开一次。
            disconnector = new Disconnector();
            disconnector.tcpTransport = this;
            if (Client.Connected)
            {
                //服务器接受设置Socket
            }
        }

        /// <summary>
        /// 开始发送接收 TODO,拆分成4个函数
        /// </summary>
        internal protected virtual void StartWork()
        {
            if (RemoteState == WorkState.NotStart)
            {
                RemoteState = WorkState.Working;
                StartSocketReceive();
                StartMessageReceive();
                StartSocketSend();
            }
        }
    }

    public partial class TcpTransport : IConnectable
    {
        /// <summary>
        /// 连接保护器，防止多次调用
        /// </summary>
        readonly object _connectlock = new object();
        /// <summary>
        /// 正在连接
        /// </summary>
        bool IsConnecting = false;
        private async Task ConnectAsync(Socket socket, IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
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
                if (endPoint.Equals(socket.RemoteEndPoint))
                {
                    return;
                }
                else
                {
                    throw new ArgumentException("socket已经连接");
                }
            }

            while (retryCount >= 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException("连接被取消");
                }
                try
                {
                    await socket.ConnectAsync(endPoint).ConfigureAwait(false);
                    IsConnecting = false;
                    disconnector.tcpTransport = this;
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

        public virtual Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            ConnectIPEndPoint = endPoint;
            if (Client == null)
            {
                SetSocket(new Socket(SocketType.Stream, ProtocolType.Tcp));
            }
            return ConnectAsync(Client, endPoint, retryCount, cancellationToken);
        }

        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {
            disconnector?.Disconnect(triggerOnDisConnect, waitSendQueue);
        }

        /// <summary>
        /// 将oldRemote的SendPipe和RpcLayer赋值给当前remote。
        /// </summary>
        /// <remarks>
        /// 断线重连三种方式：
        /// <para/> 1: 新创建一个Socket，设置到旧的remote中。
        /// <para/> 2：新建一个remote，并使用旧的remote的rpclayer，sendpipe。
        /// <para/> 3：新建一个remote，旧的remote使用 新remote的socket revepipe,重新激活旧的remote
        /// <para/> 只有方法2成立。重连后需要进行验证流程，需要收发消息甚至rpc功能，需要使用remote功能，所以1不成立。
        /// <para/> 收发消息后，socket ReceiveAsync已经挂起，socket已经和remote绑定，不能切换socket，所以方法3不成立。
        /// </remarks>
        public override bool ReConnectFrom(ITransportable transportable)
        {
            if (transportable is TcpTransport oldRemote)
            {
                oldRemote.StopSocketSend();
                this.StopSocketSend();
                //SendPipe 应该和 RpcLayer，待发送消息和rpc回调时对应关系。
                //但是既不能将SendPipe 放到RpcRemote中，也不能将RpcLayer放到Transport中，只能这样将就。
                SendPipe = oldRemote.SendPipe;
                RemoteCore.RpcLayer = oldRemote.RemoteCore.RpcLayer;
                StartSocketSend();
                return true;
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    public partial class TcpTransport
    {
        public virtual void Send<T>(T message, int rpcID, object options = null)
        {
            //todo 检查当前是否允许发送，可能已经处于断开阶段，不在允许新消息进入发送缓存区
            var allowSend = RemoteState == WorkState.Working || RemoteState == WorkState.NotStart;
            if (!allowSend)
            {
                //当遇到底层不能发送消息的情况下，如果时Rpc发送，直接触发Rpc异常。
                if (rpcID > 0)
                {
                    //对于已经注册了Rpc的消息,直接触发异常。
                    RemoteCore.RpcLayer.TrySetException(rpcID, new SocketException(-1));
                    return;
                }
                else
                {
                    throw new SocketException(-1);
                }
            }

            ///每个writer都是一个新的实例，所以这里是线程安全的。
            var writer = SendPipe.GetWriter();
            if (RemoteCore.TrySerialize(writer, rpcID, message, options))
            {
                //序列化成功
                var len = writer.WriteLengthOnHeader();
                //Logger?.Log($"序列化{message.GetType().Name}成功,总长度{len}");
                SendPipe.Push2Queue(writer);
            }
            else
            {
                //序列化失败
                writer.Discard();
            }

            //StartWork(); 不主动开启SendPipe.ReadNext，改为Log。精准手动控制。
            //开启接受和处理消息,StartWork中已经开启接收一次，这里每次发送在调用一次，防止意外停止接受。也可以省略的。
            FillRecvPipe(RecvPipe.Writer);
            StartReadRecvPipe(RecvPipe.Reader);
            if (IsSocketSending == false)
            {
                TraceListener?.WriteLine($"允许发送消息，但底层SendPipe.ReadNext处于关闭状态。");
            }
        }

        /// <summary>
        /// 发送管道
        /// </summary>
        /// <remarks>发送管道没有涵盖所有案例，尽量不要给外界访问</remarks>
        internal protected TcpSendPipe SendPipe { get; set; } = new TcpSendPipe();

        public bool IsSocketSending { get; protected set; }
        /// <summary>
        /// 开始读取发送管道，使用Socket发送消息
        /// </summary>
        public async void ReadSendPipe(TcpSendPipe sendPipe)
        {
            while (true)
            {
                lock (sendPipe)
                {
                    if (IsSocketSending)
                    {
                        return;
                    }

                    if (RemoteState != WorkState.Working
                        && RemoteState != WorkState.StopingWaitQueueSending)
                    {
                        return;
                    }

                    IsSocketSending = true;
                }

                try
                {
                    //todo,改为tryPeek,发送成功AdvanceOne,解决回到队列问题.
                    var target = await sendPipe.ReadNext().ConfigureAwait(false);

                    if (RemoteState != WorkState.Working
                        && RemoteState != WorkState.StopingWaitQueueSending)
                    {
                        //拿到待发送数据时，Socket已经不能发送了
                        return;
                    }

#if NET5_0_OR_GREATER
                    var length = target.BlockMemory.Length;
                    var result = await Client.SendAsync(target.BlockMemory,
                                                        SocketFlags.None).ConfigureAwait(false);
#else
                    var length = target.BlockSegment.Count;
                    var result = await Client.SendAsync(target.BlockSegment, SocketFlags.None).ConfigureAwait(false);
#endif

                    if (result == length)
                    {
                        //发送成功
                        sendPipe.Advance(target);
                    }
                    else
                    {
                        //发送不成功，result 是错误码
                        //https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.sockettaskextensions.sendasync?view=netstandard-2.0
                        disconnector?.OnSendError((SocketError)result);
                        //todo 如果错误码和要发送的字节恰好相等怎么办？
                    }

                    IsSocketSending = false;
                }
                catch (SocketException e)
                {
                    disconnector?.OnSendError((SocketError)e.ErrorCode);
                    IsSocketSending = false;
                    return;
                }
            }
        }

        public void StartSocketSend()
        {
            ReadSendPipe(SendPipe);
        }

        public void StopSocketSend()
        {
            SendPipe.CancelPendingRead();
            IsSocketSending = false;
        }
    }

    public partial class TcpTransport : IReceiveMessage
    {
        /// <summary>
        /// 不使用线程同步上下文，全部推送到线程池调用。useSynchronizationContext 用来保证await前后线程一致。
        /// <para/>
        /// FlushAsync后，另一头的触发是通过ThreadPoolScheduler来触发的，不是调用FlushAsync的线程，
        /// 所以useSynchronizationContext = false时，不用担心 IOCP线程 执行pipeReader，反序列化等造成IOCP线程阻塞问题。
        /// <para>--------</para>
        /// 背压和流量控制,根据项目需要在PipeOptions中设置参数。
        /// https://www.cnblogs.com/xxfy1/p/9290235.html
        /// </summary>
        /// <remarks>
        /// <para/>useSynchronizationContext 如果为true的话，
        /// <para/>那么pipe read write 异步后续只会在调用线程执行。
        /// <para/>构造 连接 StartWork调用链通常导致pipe异步后续在unity中会被锁定在主线程。
        /// <para/>https://source.dot.net/#System.IO.Pipelines/System/IO/Pipelines/PipeAwaitable.cs,115
        /// <para/>https://zhuanlan.zhihu.com/p/39223648
        /// </remarks>
        protected Pipe RecvPipe { get; set; } = new Pipe(new PipeOptions(useSynchronizationContext: false));

        /// <summary>
        /// 当前socket是不是在接收。
        /// </summary>
        public bool IsSocketReceiving { get; protected set; }
        public void StartSocketReceive()
        {
            FillRecvPipe(RecvPipe.Writer);
        }

        public void StopSocketReceive()
        {
            //Client.Shutdown
            //没有办法停止Client.ReceiveAsync，即时停止了也会触发Recv0
        }

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
                    if (IsSocketReceiving)
                    {
                        return;
                    }

                    if (RemoteState != WorkState.Working)
                    {
                        return;
                    }
                    IsSocketReceiving = true;
                }

                int queryCount = 8192;
                var buffer = pipeWriter.GetMemory(queryCount);
                int count = 0;

                try
                {
                    //大约找到了NETSTANDARD2_1_OR_GREATER 这个API 在netstandard2.0 中没有，以前使用旧的写法没问题，
                    //最近切换到NETSTANDARD2_1  这个API就失效了。
                    //源码放在unity中NETSTANDARD2_1_OR_GREATER 这个宏unity不识别，仍然用的旧API，所以源码在unity中正常。
                    //大约就是这样。 切换到新api有很多，具体不知道是哪一个有问题。暂时只能全用旧的写法
#if NET5_0_OR_GREATER
                    count = await Client.ReceiveAsync(buffer, SocketFlags.None)
                        .ConfigureAwait(false);
#else

                    if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
                    {
                        //重设长度 这里使用queryCount而不是buffer.Length。不能完全保证buffer.Length等于queryCount。
                        //修复一个可能的接受长度错误，当接收pipe取得Memory长度大于queryCount时，可能造成接受数据丢失或者数组越界。
                        segment = new ArraySegment<byte>(segment.Array, segment.Offset, queryCount);
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
                    }

                    LastReceiveTime = DateTimeOffset.UtcNow;
                    // Make the data available to the PipeReader
                    FlushResult result = await pipeWriter.FlushAsync().ConfigureAwait(false);
                    IsSocketReceiving = false;
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                catch (SocketException e)
                {
                    disconnector?.OnRecvError((SocketError)e.ErrorCode);
                    IsSocketReceiving = false;
                    return;
                }


            }
        }

        public void StartMessageReceive()
        {
            StartReadRecvPipe(RecvPipe.Reader);
        }

        /// <summary>
        /// 此方法没有经过测试
        /// </summary>
        public void StopMessageReceive()
        {
            RecvPipe.Reader.CancelPendingRead();
            IsMessageReceiving = false;
        }

        /// <summary>
        /// 正在处理消息
        /// </summary>
        public bool IsMessageReceiving { get; protected set; }
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
                    if (IsMessageReceiving)
                    {
                        return;
                    }
                    IsMessageReceiving = true;
                }
                var result = await pipeReader.ReadAsync().ConfigureAwait(false);

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
                        var nextSegmentLength = unDealBuffer.ReadInt(offset); //读取长度记得加上偏移

                        if (unReadLenght >= nextSegmentLength)
                        {
                            //取得消息体
                            var body = unDealBuffer.Slice(offset + 4, nextSegmentLength - 4);

                            //先计数后处理，如果某个数据段出现错误可以略过该段
                            unReadLenght -= nextSegmentLength;
                            offset += nextSegmentLength;
                            RemoteCore.ProcessBody(body);
                        }
                        else
                        {
                            //半包，继续读取
                            if (nextSegmentLength > 1024 * 256)
                            {
                                //todo，长度非常大可能是一个未知错误。
                                TraceListener?.WriteLine($"nextSegmentLength > 1024 * 256,长度非常大可能是一个未知错误。");
                            }
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    TraceListener?.WriteLine(e.ToString());
                }

                //标记已使用数据，要先使用在标记，不然数据可能就被释放了
                var pos = result.Buffer.GetPosition(offset);
                //这里必须标记已检查位置到Buffer.End，
                //不然如果遇到半包，Socket又迟迟收不到新数据的情况，
                //会导致pipeReader.ReadAsync()同步完成造成死循环。
                //从而冻结线程池，CPU会100%，冻结整个程序。
                pipeReader.AdvanceTo(pos, result.Buffer.End);

                IsMessageReceiving = false;

                if (result.IsCompleted || result.IsCanceled)
                {
                    //pipeReader.AdvanceTo(result.Buffer.End);
                    break;
                }
            }
        }

        public DateTimeOffset LastReceiveTime { get; protected set; } = DateTimeOffset.UtcNow;
    }
}
