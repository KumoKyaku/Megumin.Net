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
    public class UdpRemoteListener : UdpClient
    {
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint { get; }

        public UdpRemoteListener(int port, AddressFamily addressFamily = AddressFamily.InterNetworkV6)
            : base(port, addressFamily)
        {
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
                var res = await ReceiveAsync();
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
                    if (!connected.TryGetValue(res.RemoteEndPoint, out var remote))
                    {
                        remote = new UdpRemote(this);
                        connected[res.RemoteEndPoint] = remote;
                    }

                    remote.Deal(res);
                }
                else
                {
                    await Task.Yield();
                }
            }

        }

        /// <summary>
        /// 正在连接的
        /// </summary>
        readonly Dictionary<IPEndPoint, UdpRemote> connected = new Dictionary<IPEndPoint, UdpRemote>();

        public void ListenAsync()
        {
            IsListening = true;
            Task.Factory.StartNew(AcceptAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(Deal, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            IsListening = false;
        }

        /// <summary>
        /// 移除逻辑
        /// </summary>
        /// <param name="remote"></param>
        internal void Lost(UdpRemote remote)
        {

        }

        readonly Dictionary<Guid, UdpRemote> lut = new Dictionary<Guid, UdpRemote>();




        /// <summary>
        /// 根据IPEndPoint 取得对应Remote。
        /// 如果没有，先请求验证旧Remote,没有旧的，就生产一个新的。
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        /// <remarks>
        /// 因为UDP需求是客户端换IP后，要能找到对应实例，所以要有另外的Key，还有防止碰撞攻击。
        /// todo,使用int 做key，使用多个guid做密钥。
        /// </remarks>
        ValueTask<object> GetRemote(IPEndPoint endPoint)
        {
            if (connected.TryGetValue(endPoint, out var remote))
            {
                return new ValueTask<object>(remote);
            }
            else
            {
                //没有已知IPEndPoint，发送验证包
                //验证包是一个Guid
                Guid guid = new Guid();
                byte[] valid = new byte[10];
                Send(valid, 10, endPoint);

                //4字节识别头 + int ID + GUID 16字节

                //等待回复验证  todo
                //回复包是两个Guid，第一个是验证包原样返回，第二个包是旧Guid，如果没有旧的，返回验证Guid。
                Guid validReply = default;
                Guid oldGuid = default;
                int version = 0;//添加一个ID version,防止时序出错。

                if (validReply == guid)
                {
                    UdpRemote udp = null;
                    //验证回复有效
                    if (validReply == oldGuid)
                    {
                        //没有旧Guid
                        //生产新Remote
                        udp = new UdpRemote();
                    }
                    else
                    {
                        udp = lut[oldGuid];
                        lut.Remove(oldGuid);
                    }

                    udp.GUID = validReply;
                    lut.Add(validReply, udp);

                    return new ValueTask<object>(udp);
                }
                else
                {
                    //验证回复无效
                    return new ValueTask<object>(null);
                }
            }
        }

        readonly Dictionary<int, UdpConnector> cont = new Dictionary<int, UdpConnector>();
        public class UdpConnector
        {
            public UdpConnector(UdpRemoteListener listener, IPEndPoint endPoint)
            {
                this.listener = listener;
                RemoteEndPoint = new IPEndPoint(endPoint.Address, endPoint.Port);
                ID = InterlockedID<UdpConnector>.NewID(1000); //从1001开始，没什么特殊目的
                password = Guid.NewGuid();
                listener.cont.Add(ID, this);
            }

            UdpRemoteListener listener;
            public readonly IPEndPoint RemoteEndPoint;
            public int ID { get; }
            public Guid password { get; }

            public UDPServerSide udp;
            /// <summary>
            /// 验证失败次数
            /// </summary>
            public int VerifyDefeatedCount = 0;
            /// <summary>
            /// 是不是已经失去连接
            /// </summary>
            public bool IsLost = false;
            /// <summary>
            /// 是不是绑定到新连接
            /// </summary>
            public bool IsBind2NewConnect = false;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="buffer"></param>
            /// <returns>是不是验证消息返回</returns>
            public bool VerifyResp(byte[] buffer)
            {
                bool isVerifyResp = false;
                if (buffer.Length == 48)
                {
                    Span<byte> resp = buffer;
                    int firstMarker = resp.ReadInt();
                    if (firstMarker == int.MaxValue)
                    {
                        int endMarker = resp.Slice(resp.Length - 4).ReadInt();
                        if (endMarker != int.MaxValue)
                        {
                            //符合验证返回消息格式
                            isVerifyResp = true;
                            int reqID = resp.Slice(4).ReadInt();
                            Guid reqPW = resp.Slice(8).ReadGuid();
                            if (reqID == ID && reqPW == password)
                            {
                                //返回消息有效，验证成功。
                                int oldID = resp.Slice(40).ReadInt();
                                Guid oldPW = resp.Slice(44).ReadGuid();

                                var needCreateNewUdp = false;
                                if (listener.cont.TryGetValue(oldID,out var oldconn))
                                {
                                    if (oldconn.password == oldPW)
                                    {
                                        //密码验证通过，将旧UDP迁移到当前连接。旧IP保留一会，定时清理。
                                        udp = oldconn.udp;
                                        oldconn.IsBind2NewConnect = true;
                                        IsValid = true;
                                    }
                                    else
                                    {
                                        //旧密码验证不通过,可能是碰撞攻击
                                        needCreateNewUdp = true;
                                    }
                                }
                                else
                                {
                                    //没有旧连接
                                    needCreateNewUdp = true;
                                }

                                if (needCreateNewUdp)
                                {
                                    CreateNewUdp();
                                }
                            }
                            else
                            {
                                //返回消息无效，验证失败。
                                VerifyDefeatedCount++;
                            }
                        }
                        else
                        {
                            //不是验证返回消息格式
                        }
                    }
                    else
                    {
                        //不是验证返回消息格式
                    }
                }

                return isVerifyResp;
            }

            private void CreateNewUdp()
            {
                udp = new UDPServerSide();
                IsValid = true;
            }

            bool IsValid = false;
            private TaskCompletionSource<UdpReceiveResult> authSource;

            public async void Deal(IPEndPoint endPoint,byte[] buffer)
            {
                if (!IsValid)
                {
                    if (VerifyResp(buffer))
                    {
                        //
                        return;
                    }
                    else
                    {
                        await Auth(endPoint);
                    }
                }

                if (IsValid)
                {
                    udp.Deal(endPoint, buffer);
                }
            }

            /// <summary>
            /// 44字节请求验证消息。
            /// </summary>
            public const int RquestValidMessageLength = 44;
            ValueTask Auth(IPEndPoint endPoint)
            {
                byte[] valid = new byte[RquestValidMessageLength]; 
                Span<byte> request = valid;
                int.MaxValue.WriteTo(request); //4 识别符
                ID.WriteTo(request.Slice(4));  //4ID
                password.WriteTo(request.Slice(8)); // 16 guid 密钥
                //留空16个字节备用。
                int.MaxValue.WriteTo(request.Slice(40));//4 识别尾
                listener.Send(valid, RquestValidMessageLength, endPoint);
            }
        }
    }

    

    public class UdpHandle
    {
        public int ID { get; set; }
        public Guid password { get; set; }
        public int OldID { get; set; }
        public Guid oldPW { get; set; }
    }
}

//namespace Megumin.Remote
//{
//    /// <summary>
//    /// IPV4 IPV6 udp中不能混用
//    /// </summary>
//    public class UdpRemoteListener : UdpClient
//    {
//        public IPEndPoint ConnectIPEndPoint { get; set; }
//        public EndPoint RemappedEndPoint { get; }

//        public UdpRemoteListener(int port, AddressFamily addressFamily = AddressFamily.InterNetworkV6)
//            : base(port, addressFamily)
//        {
//            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None, port);
//        }

//        public bool IsListening { get; private set; }
//        public TaskCompletionSource<UdpRemote> TaskCompletionSource { get; private set; }

//        async void AcceptAsync()
//        {
//            while (IsListening)
//            {
//                var res = await ReceiveAsync();
//                var (Size, MessageID) = MessagePipeline.Default.ParsePacketHeader(res.Buffer);
//                if (MessageID == MSGID.UdpConnectMessageID)
//                {
//                    ReMappingAsync(res);
//                }
//            }
//        }

//        /// <summary>
//        /// 正在连接的
//        /// </summary>
//        readonly Dictionary<IPEndPoint, UdpRemote> connecting = new Dictionary<IPEndPoint, UdpRemote>();
//        /// <summary>
//        /// 连接成功的
//        /// </summary>
//        readonly ConcurrentQueue<UdpRemote> connected = new ConcurrentQueue<UdpRemote>();
//        /// <summary>
//        /// 重映射
//        /// </summary>
//        /// <param name="res"></param>
//        private async void ReMappingAsync(UdpReceiveResult res)
//        {
//            if (!connecting.TryGetValue(res.RemoteEndPoint, out var remote))
//            {
//                remote = new UdpRemote(this.Client.AddressFamily);
//                connecting[res.RemoteEndPoint] = remote;

//                var (Result, Complete) = await remote.TryAccept(res).WaitAsync(5000);

//                if (Complete)
//                {
//                    ///完成
//                    if (Result)
//                    {
//                        ///连接成功
//                        if (TaskCompletionSource == null)
//                        {
//                            connected.Enqueue(remote);
//                        }
//                        else
//                        {
//                            TaskCompletionSource.SetResult(remote);
//                        }
//                    }
//                    else
//                    {
//                        ///连接失败但没有超时
//                        remote.Dispose();
//                    }
//                }
//                else
//                {
//                    ///超时，手动断开，释放remote;
//                    remote.Disconnect();
//                    remote.Dispose();
//                }
//            }
//        }

//        public async Task<UdpRemote> ListenAsync(ReceiveCallback receiveHandle)
//        {
//            IsListening = true;
//            System.Threading.ThreadPool.QueueUserWorkItem(state =>
//            {
//                AcceptAsync();
//            });

//            if (connected.TryDequeue(out var remote))
//            {
//                if (remote != null)
//                {
//                    remote.ReceiveStart();
//                    return remote;
//                }
//            }
//            if (TaskCompletionSource == null)
//            {
//                TaskCompletionSource = new TaskCompletionSource<UdpRemote>();
//            }

//            var res = await TaskCompletionSource.Task;
//            TaskCompletionSource = null;
//            res.MessagePipeline = MessagePipeline.Default;
//            res.OnReceiveCallback += receiveHandle;
//            res.ReceiveStart();
//            return res;
//        }

//        /// <summary>
//        /// 在ReceiveStart调用之前设置pipline.
//        /// </summary>
//        /// <param name="pipline"></param>
//        /// <returns></returns>
//        public async Task<UdpRemote> ListenAsync(ReceiveCallback receiveHandle, IMessagePipeline pipline)
//        {
//            IsListening = true;
//            System.Threading.ThreadPool.QueueUserWorkItem(state =>
//            {
//                AcceptAsync();
//            });

//            if (connected.TryDequeue(out var remote))
//            {
//                if (remote != null)
//                {
//                    remote.MessagePipeline = pipline;
//                    remote.ReceiveStart();
//                    return remote;
//                }
//            }
//            if (TaskCompletionSource == null)
//            {
//                TaskCompletionSource = new TaskCompletionSource<UdpRemote>();
//            }

//            var res = await TaskCompletionSource.Task;
//            TaskCompletionSource = null;
//            res.MessagePipeline = pipline;
//            res.OnReceiveCallback += receiveHandle;
//            res.ReceiveStart();
//            return res;
//        }

//        public void Stop()
//        {
//            IsListening = false;
//        }
//    }
//}
