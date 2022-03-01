using Net.Remote;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


///如果服务端只用一个udp接收所有客户端数据
///极限情况下物理机万兆网卡，怎么想一个socket udp接收缓冲区也不够
///https://stackoverflow.com/questions/57431090/does-c-sharp-udp-sockets-receivebuffersize-applies-to-size-of-datagrams-or-size
///这一版现状先把socket recvsize调大试试，看看丢包情况
///
///https://blog.csdn.net/zhyh3737/article/details/7219275
///
namespace Megumin.Remote
{
    /// <summary>
    /// 2018年时IPV4 IPV6 udp中不能混用，不知道现在情况
    /// </summary>
    public class UdpRemoteListener : UdpClient, IListener<UdpRemote>
    {
        public IPEndPoint ConnectIPEndPoint { get; set; }

        protected readonly Dictionary<IPEndPoint, UdpRemote> connected = new Dictionary<IPEndPoint, UdpRemote>();
        protected readonly Dictionary<Guid, UdpRemote> lut = new Dictionary<Guid, UdpRemote>();
        protected readonly Dictionary<IPEndPoint, TaskCompletionSource<UdpAuthResponse>> authing
            = new Dictionary<IPEndPoint, TaskCompletionSource<UdpAuthResponse>>();
        /// <remarks>
        /// Q:要不要用同步队列，预计有多个线程入队，只有一个线程出队，会不会有线程安全问题？
        /// </remarks>
        protected Queue<UdpReceiveResult> UdpReceives = new Queue<UdpReceiveResult>();

        /// <summary>
        /// 服务端使用20个Socket向客户端发送.
        /// <para/> TODO NAT情况复杂，可能无法发送 https://www.cnblogs.com/mq0036/p/4644776.html
        /// <para/> (1)完全Cone NAT 无论目标地址和端口怎样，每次都把该私有源IP地址/端口映射到同一个全局源地址/端口；外网的任何主机都可以发送报文到该映射的全局地址而访问到该内部主机。路由器的静态地址映射就是属于这种。
        ///(2)限制Cone NAT 地址/端口映射的情况同完全Cone NAT的，但外网的主机要访问内网主机，该内网主机必须先发送过报文给该外网主机的地址。
        ///(3)端口限制Cone NAT 地址/端口映射情况同完全Cone NAT的，但外网主机要访问内网主机，该内网主机必须先发送过报文给该外网主机的地址和端口。大多数路由器的NAPT就是属于这种情况。本文后面论及的Cone NAT也是指这种情况。
        ///(4)Symmetric NAT 对不同的目标地址/端口，源私有地址映射到源全局地址不变，但是映射的全局端口会改变。外网主机必须先收到过内网主机的报文，才能访问到该内网主机。一些路由器和防火墙产品的NAT就是属于这种情况。
        /// <para/> 1,2是没问题的，3通常需要客户端先发送一个消息到发送端口，不然SendSockets由于和listen端口不一致，会被NAT丢弃消息。4则完全没有办法。
        /// 需要一个测试方法测试连接是否支持SendSockets发送
        /// 最开始可以先用listen端口发送，异步测试是否支持，等到能支持时转到SendSockets发送。，不支持必须使用 listen端口发送。
        /// </summary>
        protected Socket[] SendSockets = new Socket[20];
        public UdpRemoteListener(int port)
            : base(port)
        {
            Init(port);
        }

        public UdpRemoteListener(int port, AddressFamily addressFamily)
            : base(port, addressFamily)
        {
            Init(port);
        }

        private void Init(int port, AddressFamily addressFamily = AddressFamily.InterNetwork)
        {
            for (int i = 0; i < SendSockets.Length; i++)
            {
                SendSockets[i] = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
            }
            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None, port);
            Client.ReceiveBufferSize = 1020 * 1024 * 5; //先设个5mb看看
        }

        public bool IsListening { get; private set; }

        ///<remarks>
        ///Q：如果同时调用多次ReceiveAsync有没有实际意义？能不能达到加速接收的目的？
        ///</remarks>
        async void AcceptAsync()
        {
            while (IsListening)
            {
                var res = await ReceiveAsync().ConfigureAwait(false);
                UdpReceives.Enqueue(res);
            }
        }

        /// <summary>
        /// 接收和处理分开
        /// </summary>
        async void Deal()
        {
            while (IsListening)
            {
                if (UdpReceives.Count > 0)
                {
                    var res = UdpReceives.Dequeue();
                    IPEndPoint endPoint = res.RemoteEndPoint;
                    byte[] recvbuffer = res.Buffer;
                    InnerDeal(endPoint, recvbuffer);
                }
                else
                {
                    await Task.Yield();
                }
            }

        }

        protected virtual async void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer)
        {
            byte messageType = recvbuffer[0];
            switch (messageType)
            {
                case UdpRemoteMessageDefine.UdpAuthRequest:
                    //被动侧不处理主动侧提出的验证。
                    break;
                case UdpRemoteMessageDefine.UdpAuthResponse:
                    DealAnswerBuffer(endPoint, recvbuffer);
                    break;
                case UdpRemoteMessageDefine.LLMsg:
                case UdpRemoteMessageDefine.Common:
                    var remote = await FindRemote(endPoint).ConfigureAwait(false);
                    if (remote != null)
                    {
                        remote.ServerSideRecv(endPoint, recvbuffer, 0, recvbuffer.Length);
                    }
                    break;
                default:
                    break;
            }
        }

        protected async ValueTask<UdpRemote> FindRemote(IPEndPoint endPoint)
        {
            if (connected.TryGetValue(endPoint, out var remote))
            {
                return remote;
            }
            else
            {
                var answer = await BaginNewAuth(endPoint).ConfigureAwait(false);
                lock (lut)
                {
                    if (lut.TryGetValue(answer.Guid, out var udpRemote))
                    {
                        if (udpRemote.Password != answer.Password)
                        {
                            //guid和密码不匹配,可能遇到有人碰撞攻击
                            return null;
                        }
                        else
                        {
                            if (udpRemote.ConnectIPEndPoint != endPoint)
                            {
                                //重绑定远端
                                connected.Remove(udpRemote.ConnectIPEndPoint);
                                udpRemote.ConnectIPEndPoint = endPoint;
                                connected.Add(endPoint, udpRemote);
                            }

                            return udpRemote;
                        }
                    }
                    else
                    {
                        UdpRemote udp = CreateNew(endPoint, answer);
                        if (udp == null)
                        {
                            DebugLogger.LogWarning($"Listner 无法创建 remote");
                        }
                        return udp;
                    }
                }
            }
        }

        protected virtual UdpRemote CreateNew(IPEndPoint endPoint, UdpAuthResponse answer)
        {
            if (remoteCreators.TryDequeue(out var creator))
            {
                var (continueAction, udp) = creator.Invoke();

                if (udp != null)
                {
                    udp.Listener = this;
                    udp.IsVaild = true;
                    udp.ConnectIPEndPoint = endPoint;
                    udp.GUID = answer.Guid;
                    udp.Password = answer.Password;
                    //todo add listenUdpclient.
                    udp.Client = SendSockets[connected.Count % SendSockets.Length];
                    lut.Add(udp.GUID, udp);
                    connected.Add(endPoint, udp);
                }

                continueAction?.Invoke();
                return udp;
            }

            return null;
        }

        Random random = new Random();
        ValueTask<UdpAuthResponse> BaginNewAuth(IPEndPoint endPoint)
        {
            UdpAuthRequest session = new UdpAuthRequest();
            session.Guid = Guid.NewGuid();
            session.Password = random.Next();
            //创建认证消息
            byte[] buffer = new byte[UdpAuthRequest.Length];
            session.Serialize(buffer);
            Send(buffer, buffer.Length, endPoint);
            if (!authing.TryGetValue(endPoint, out var source))
            {
                source = new TaskCompletionSource<UdpAuthResponse>();
                authing.Add(endPoint, source);
            }
            return new ValueTask<UdpAuthResponse>(source.Task);
        }


        public void DealAnswerBuffer(IPEndPoint endPoint, Span<byte> buffer)
        {
            if (authing.TryGetValue(endPoint, out var source))
            {
                authing.Remove(endPoint);
                var answer = UdpAuthResponse.Deserialize(buffer);
                source.TrySetResult(answer);
            }
        }

        public void Stop()
        {
            IsListening = false;
        }

        protected ConcurrentQueue<Func<(Action ContinueDelegate, UdpRemote Remote)>>
            remoteCreators = new ConcurrentQueue<Func<(Action ContinueDelegate, UdpRemote Remote)>>();

        public virtual ValueTask<R> ListenAsync<R>(Func<R> createFunc) where R : UdpRemote
        {
            if (IsListening == false)
            {
                IsListening = true;
                Task.Run(AcceptAsync);
                Task.Run(Deal);
            }
            TaskCompletionSource<R> source = new TaskCompletionSource<R>();

            Func<(Action, UdpRemote)> d = () =>
            {
                var r = createFunc.Invoke();
                Action a = () => { source.SetResult(r); };
                return (a, r);
            };

            remoteCreators.Enqueue(d);

            return new ValueTask<R>(source.Task);
        }
    }
}

