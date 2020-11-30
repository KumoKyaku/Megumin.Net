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
    public class UdpRemoteMessageDefine
    {
        public const byte QuestAuth = 10;
        public const byte Answer = 20;
        public const byte Test = 30;
        public const byte Common = 40;
    }

    /// <summary>
    /// Udp认证请求
    /// </summary>
    public struct UdpAuthRequest
    {
        public const int Length = 21;
        public Guid Guid;
        public int Password;

        public void Serialize(Span<byte> span)
        {
            span[0] = UdpRemoteMessageDefine.QuestAuth;
            Guid.WriteTo(span.Slice(1));
            Password.WriteTo(span.Slice(17));
        }

        public static UdpAuthRequest Deserialize(Span<byte> span)
        {
            if (span[0] != UdpRemoteMessageDefine.QuestAuth)
            {
                throw new FormatException();
            }
            UdpAuthRequest auth = new UdpAuthRequest();
            auth.Guid = span.Slice(1).ReadGuid();
            auth.Password = span.Slice(17).ReadInt();
            return auth;
        }
    }

    /// <summary>
    /// Udp认证应答
    /// </summary>
    public struct UdpAuthResponse
    {
        public const int Length = 26;
        public bool IsNew;
        public Guid Guid;
        public int Password;
        public int KcpChannel;

        public void Serialize(Span<byte> span)
        {
            span[0] = UdpRemoteMessageDefine.Answer;
            span[1] = (byte)(IsNew ? 1 : 0);
            Guid.WriteTo(span.Slice(2));
            Password.WriteTo(span.Slice(18));
            KcpChannel.WriteTo(span.Slice(22));
        }

        public static UdpAuthResponse Deserialize(Span<byte> span)
        {
            if (span[0] != UdpRemoteMessageDefine.Answer)
            {
                throw new FormatException();
            }
            UdpAuthResponse answer = new UdpAuthResponse();
            answer.IsNew = span[1] != 0;
            answer.Guid = span.Slice(2).ReadGuid();
            answer.Password = span.Slice(18).ReadInt();
            answer.KcpChannel = span.Slice(22).ReadInt();
            return answer;
        }
    }

    /// <summary>
    /// 2018年时IPV4 IPV6 udp中不能混用，不知道现在情况
    /// </summary>
    public class UdpRemoteListener : UdpClient
    {
        public IPEndPoint ConnectIPEndPoint { get; set; }

        readonly Dictionary<IPEndPoint, UdpRemote> connected = new Dictionary<IPEndPoint, UdpRemote>();
        readonly Dictionary<Guid, UdpRemote> lut = new Dictionary<Guid, UdpRemote>();
        readonly Dictionary<IPEndPoint, TaskCompletionSource<UdpAuthResponse>> authing
            = new Dictionary<IPEndPoint, TaskCompletionSource<UdpAuthResponse>>();


        public UdpRemoteListener(int port, AddressFamily addressFamily = AddressFamily.InterNetwork)
            : base(port, addressFamily)
        {
            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None, port);
            Client.ReceiveBufferSize = 1020 * 1024 * 5; //先设个5mb看看
        }

        private Func<UdpRemote> CreateFunc;

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

        /// <remarks>
        /// Q:要不要用同步队列，预计有多个线程入队，只有一个线程出队，会不会有线程安全问题？
        /// </remarks>
        protected Queue<UdpReceiveResult> UdpReceives = new Queue<UdpReceiveResult>();

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

        private async void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer)
        {
            byte messageType = recvbuffer[0];
            switch (messageType)
            {
                case UdpRemoteMessageDefine.QuestAuth:
                    //被动侧不处理主动侧提出的验证。
                    break;
                case UdpRemoteMessageDefine.Answer:
                    DealAnswerBuffer(endPoint, recvbuffer);
                    break;
                case UdpRemoteMessageDefine.Test:
                case UdpRemoteMessageDefine.Common:
                    var remote = await FindRemote(endPoint);
                    if (remote != null)
                    {
                        remote.ServerSideRecv(endPoint, recvbuffer,1, recvbuffer.Length -1);
                    }
                    break;
                default:
                    break;
            }
        }

        async ValueTask<UdpRemote> FindRemote(IPEndPoint endPoint)
        {
            if (connected.TryGetValue(endPoint, out var remote))
            {
                return remote;
            }
            else
            {
                var answer = await BaginNewAuth(endPoint);
                if (lut.TryGetValue(answer.Guid,out var udpRemote))
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
                    UdpRemote udp = CreateFunc?.Invoke();
                    if (udp == null)
                    {
                        udp = new UdpRemote();
                    }
                    udp.ConnectIPEndPoint = endPoint;
                    udp.GUID = answer.Guid;
                    udp.Password = answer.Password;
                    lut.Add(udp.GUID, udp);
                    connected.Add(endPoint, udp);
                    OnAccept(udp);
                    return udp;
                }
            }
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
            if (!authing.TryGetValue(endPoint,out var source))
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
        
        public void ListenAsync<T>(Func<T> createFunc)
            where T : UdpRemote
        {
            this.CreateFunc = createFunc;
            IsListening = true;
            Task.Factory.StartNew(AcceptAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(Deal, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            IsListening = false;
        }


        public virtual void OnAccept(UdpRemote remote)
        {

        }


    }
}

