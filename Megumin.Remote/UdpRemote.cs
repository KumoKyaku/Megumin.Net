using Megumin.Message;
using Megumin.Remote.Rpc;
using Net.Remote;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;
using static Megumin.Remote.TcpRemote;
using static Megumin.Remote.UdpRemoteListener;

namespace Megumin.Remote
{
    /// <summary>
    /// <inheritdoc/>
    /// <para></para>
    /// Unity中必须,明确指定使用IPV4还是IPV6。无论什么平台。可能是mono的问题。
    /// <para>SocketException: Protocol option not supported</para>
    /// http://www.schrankmonster.de/2006/04/26/system-net-sockets-socketexception-protocol-not-supported/
    /// </summary>
    public partial class UdpRemote : RpcRemote, IRemoteEndPoint, IRemote, IConnectable
    {
        public int ID { get; } = InterlockedID<IRemoteID>.NewID();
        public AddressFamily? AddressFamily { get; set; } = null;
        public Guid? GUID { get; internal set; } = null;
        public int? Password { get; set; } = null;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public virtual EndPoint RemappedEndPoint => ConnectIPEndPoint;
        public EndPoint RemoteEndPoint => ConnectIPEndPoint;
        public Socket Client { get; protected set; }
        public bool IsVaild { get; internal protected set; }
        public UdpRemoteListener UdpRemoteListener { get; internal protected set; }
        /// <summary>
        /// 是不是监听侧Remote
        /// </summary>
        public bool IsListenSide { get; internal protected set; } = false;

        public float LastReceiveTimeFloat { get; }
        /// <summary>
        /// 为kcp预留
        /// </summary>
        protected int KcpIOChannel { get; set; }

        public UdpRemote(AddressFamily? addressFamily = null)
        {
            this.AddressFamily = addressFamily;
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
        }
    }

    public partial class UdpRemote
    {
        //连接相关功能

        static byte[] conn = new byte[1];
        public SocketCloser Closer { get; internal protected set; } = null;
        /// <summary>
        /// <inheritdoc/>
        /// <para></para>
        /// Unity中必须明确指定使用IPV4还是IPV6。无论什么平台。可能是mono的问题。
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="retryCount"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            if (!Password.HasValue)
            {
                Password = new Random().Next(1000, 10000);
            }
            ConnectIPEndPoint = endPoint;
            if (Client == null)
            {
                if (AddressFamily == null)
                {
                    Client = new Socket(SocketType.Dgram, ProtocolType.Udp);
                }
                else
                {
                    Client = new Socket(AddressFamily.Value, SocketType.Dgram, ProtocolType.Udp);
                }
                //每个Socket可以关闭一次。
                Closer = new SocketCloser();
            }
            var localEP = AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? IPEndPointStatics.Any : IPEndPointStatics.IPv6Any;
            Client.Bind(localEP);
            //Client.SendTo(conn, endPoint);//承担bind作用，不然不能recv。
            ConnectSideSocketReceive();

            //发送一个心跳包触发认证。
            var (_, exception) = await Send<Heartbeat>(Heartbeat.Default, HeartbeatSendOption);
            if (exception is RcpTimeoutException rcpex)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
            if (exception != null)
            {
                throw exception;
            }
        }

        //连接认证部分================================================

        /// <summary>
        /// 处理认证
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="recvbuffer"></param>
        protected virtual void DealAuthBuffer(IPEndPoint endPoint, byte[] recvbuffer)
        {
            var auth = UdpAuthRequest.Deserialize(recvbuffer);
            //创建认证回复消息
            UdpAuthResponse answer = new UdpAuthResponse();

            if (!this.GUID.HasValue)
            {
                this.GUID = auth.Guid;
            }

            answer.Guid = this.GUID.Value;
            answer.Password = Password.Value;
            answer.KcpChannel = KcpIOChannel;
            byte[] buffer = new byte[UdpAuthResponse.Length];
            answer.Serialize(buffer);
            Client.SendTo(buffer, 0, UdpAuthResponse.Length, SocketFlags.None, endPoint);
        }

        static readonly byte[] Disconnect0Buffer = new byte[0];
        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {
            if (IsListenSide)
            {
                //监听侧是公用的socket，不用做处理。
                //应该给连接端发一条特殊消息，要求连接端主动断开。
                throw new InvalidOperationException($"监听端不应该主动调用断开。");
            }
            else
            {
                ConnectSideShutdown(triggerOnDisConnect, waitSendQueue);
            }
        }

        /// <summary>
        /// 连接测主动断开，shutdown，先向对面发送一个0字节消息，对面会触发Recv0。然后关闭自己一侧Socket。
        /// </summary>
        /// <param name="triggerOnDisConnect"></param>
        /// <param name="waitSendQueue"></param>
        protected virtual async ValueTask ConnectSideShutdown(bool triggerOnDisConnect, bool waitSendQueue)
        {
            await Client.SendToAsync(new ArraySegment<byte>(Disconnect0Buffer, 0, 0), SocketFlags.None, ConnectIPEndPoint)
                                .ConfigureAwait(false);
            if (Closer != null)
            {
                Closer.TraceListener = TraceListener;
                Closer.SafeClose(Client, SocketError.Disconnecting, this, triggerOnDisConnect, waitSendQueue, options: new DisconnectOptions());
                IsVaild = false;
                Client = null;
            }
        }

        internal protected virtual void Recv0(IPEndPoint endPoint)
        {
            if (IsListenSide)
            {
                //监听侧是公用的socket，不用做处理。
                PreDisconnect(SocketError.Shutdown, null);
                OnDisconnect(SocketError.Shutdown, null);
                PostDisconnect(SocketError.Shutdown, null);
            }
            else
            {
                if (Closer != null)
                {
                    Closer.TraceListener = TraceListener;
                    Closer.OnRecv0(Client, this);
                    IsVaild = false;
                    Client = null;
                }
            }
        }

        protected static readonly SendOption HeartbeatSendOption = new SendOption()
        {
            MillisecondsTimeout = 5000,
            Cmd = 1,
            RpcComplatePost2ThreadScheduler = true,
            ForceUdp = true,
        };

        public async void SendBeat(int intervalMS = 2000, CancellationToken token = default)
        {
            int MissHearCount = 0;
            while (true)
            {
                MissHearCount += 1;
                var (_, exception) = await Send<Heartbeat>(Heartbeat.Default, HeartbeatSendOption);
                if (exception == null)
                {
                    MissHearCount = 0;
                }

                if (MissHearCount >= 5)
                {
                    MissHearCount = 0;
                    //触发断开。TODO
                    PreDisconnect(SocketError.TimedOut, ActiveOrPassive.Passive);
                    OnDisconnect(SocketError.TimedOut, ActiveOrPassive.Passive);
                    PostDisconnect(SocketError.TimedOut, ActiveOrPassive.Passive);
                    break;
                }
                await Task.Delay(intervalMS);
                if (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    public partial class UdpRemote
    {
        //发送==========================================================

        protected virtual UdpBufferWriter SendWriter { get; } = new UdpBufferWriter(8192 * 4);

        public override void Send(int rpcID, object message, object options = null)
        {
            if (Client == null || Closer?.IsDisconnecting == true)
            {
                //当遇到底层不能发送消息的情况下，如果时Rpc发送，直接触发Rpc异常。
                if (rpcID > 0)
                {
                    //对于已经注册了Rpc的消息,直接触发异常。
                    RpcLayer.RpcCallbackPool.TrySetException(rpcID, new SocketException(-1));
                    return;
                }
                else
                {
                    throw new SocketException(-1);
                }
            }

            SendWriter.WriteHeader(UdpRemoteMessageDefine.UdpData);
            if (TrySerialize(SendWriter, rpcID, message, options))
            {
                var (buffer, lenght) = SendWriter.Pop();
                SocketSend(buffer, lenght);
            }
            else
            {
                var (buffer, lenght) = SendWriter.Pop();
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 网络层实际发送数据位置
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="lenght"></param>
        protected async void SocketSend(IMemoryOwner<byte> buffer, int lenght)
        {
            if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
            {
                var target = new ArraySegment<byte>(segment.Array, 0, lenght);
                await Client.SendToAsync(target, SocketFlags.None, ConnectIPEndPoint)
                    .ConfigureAwait(false);
            }

            buffer.Dispose();
        }
    }

    public partial class UdpRemote
    {
        //接收============================================================

        public bool IsSocketReceiving { get; protected set; }
        protected readonly object ConnectSideSocketReceiveLock = new object();
        /// <summary>
        /// 主动侧需要手动开启接收，被动侧由listener接收然后分发
        /// </summary>
        /// <param name="port"></param>
        public async void ConnectSideSocketReceive()
        {
            lock (ConnectSideSocketReceiveLock)
            {
                if (IsSocketReceiving)
                {
                    return;
                }
                IsSocketReceiving = true;
            }

            //Client.Bind(new IPEndPoint(IPAddress.Any, port));
            IsVaild = true;

            var remoteEndPoint = AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? IPEndPointStatics.Any : IPEndPointStatics.IPv6Any;
            while (true)
            {
                var cache = ArrayPool<byte>.Shared.Rent(0x10000);
                try
                {
                    ArraySegment<byte> buffer = new ArraySegment<byte>(cache);
                    var res = await Client.ReceiveFromAsync(buffer, SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
                    InnerDeal(res.RemoteEndPoint as IPEndPoint, cache, 0, res.ReceivedBytes);
                }
                catch (ObjectDisposedException e)
                {
                    //断开连接时触发
                    TraceListener?.WriteLine(e);
                    break;
                }
                catch (Exception e)
                {
                    TraceListener?.WriteLine(e);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(cache);
                }
            }

            lock (ConnectSideSocketReceiveLock)
            {
                IsSocketReceiving = false;
            }
        }

        protected virtual void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer, int start, int count)
        {
            byte messageType = recvbuffer[start];
            switch (messageType)
            {
                case UdpRemoteMessageDefine.UdpAuthRequest:
                    DealAuthBuffer(endPoint, recvbuffer);
                    break;
                case UdpRemoteMessageDefine.UdpAuthResponse:
                    //主动侧不处理验证应答。
                    break;
                case UdpRemoteMessageDefine.LLData:
                    RecvLLData(endPoint, recvbuffer, start + 1, count - 1);
                    break;
                case UdpRemoteMessageDefine.UdpData:
                    RecvUdpData(endPoint, recvbuffer, start + 1, count - 1);
                    break;
                case UdpRemoteMessageDefine.KcpData:
                    RecvKcpData(endPoint, recvbuffer, start + 1, count - 1);
                    break;
                default:
                    break;
            }
        }

        internal protected virtual void RecvLLData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {
            ProcessBody(new ReadOnlySequence<byte>(buffer, start, count));
        }

        internal protected virtual void RecvUdpData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {
            ProcessBody(new ReadOnlySequence<byte>(buffer, start, count));
        }

        internal protected virtual void RecvKcpData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {

        }

        internal protected virtual void RecvLLData(IPEndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            ProcessBody(buffer);
        }

        internal protected virtual void RecvUdpData(IPEndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            ProcessBody(buffer);
        }

        internal protected virtual void RecvKcpData(IPEndPoint endPoint, ReadOnlySpan<byte> buffer)
        {

        }
    }
}
