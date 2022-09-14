using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{

    /// <summary>
    /// todo 连接kcpid
    /// </summary>
    public partial class KcpRemote : UdpRemote
    {
        IKcpIO kcp = null;
        IKcpUpdate kcpUpdate = null;
        protected int KcpIOChannel { get; set; }
        const int BufferSizer = 1024 * 4;
        public void InitKcp(int kcpChannel)
        {
            if (kcp == null)
            {
                KcpIOChannel = kcpChannel;
                KcpIO kcpIO = new KcpIO((uint)KcpIOChannel);
                kcp = kcpIO;
                kcpUpdate = kcpIO;
                allkcp.Add(new WeakReference<IKcpUpdate>(kcpUpdate));
                KCPUpdate();
                KcpOutput();
                KCPRecv();
            }
        }

        //循环Tick================================================================
        static List<WeakReference<IKcpUpdate>>
            allkcp = new List<WeakReference<IKcpUpdate>>();
        static bool IsGlobalUpdate = false;
        protected async void KCPUpdate()
        {
            lock (allkcp)
            {
                if (IsGlobalUpdate)
                {
                    return;
                }
                IsGlobalUpdate = true;
            }

            while (true)
            {
                var time = DateTime.UtcNow;
                allkcp.RemoveAll(
                    item =>
                    {
                        if (item.TryGetTarget(out var update))
                        {
                            update?.Update(time);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                );
                await Task.Delay(5);
            }
        }

        // 认证===================================================================
        protected override void DealAuthBuffer(IPEndPoint endPoint, byte[] recvbuffer)
        {
            var auth = UdpAuthRequest.Deserialize(recvbuffer);
            //创建认证回复消息
            UdpAuthResponse answer = new UdpAuthResponse();

            if (Password == -1)
            {
                this.GUID = auth.Guid;
                this.Password = auth.Password;
                answer.IsNew = true;
            }

            answer.Guid = this.GUID;
            answer.Password = Password;
            answer.KcpChannel = KcpIOChannel;
            byte[] buffer = new byte[UdpAuthResponse.Length];
            answer.Serialize(buffer);
            Client.SendTo(buffer, 0, UdpAuthResponse.Length, SocketFlags.None, endPoint);
        }
        // 发送===================================================================
        protected Writer kcpout = new Writer(BufferSizer);
        async void KcpOutput()
        {
            while (true)
            {
                kcpout.WriteHeader(UdpRemoteMessageDefine.Common);
                await kcp.Output(kcpout).ConfigureAwait(false);
                var (buffer, lenght) = kcpout.Pop();
                SocketSend(buffer, lenght);
            }
        }

        public override void Send(int rpcID, object message, object options = null)
        {
            if (TrySerialize(SendWriter, rpcID, message, options))
            {
                var (buffer, lenght) = SendWriter.Pop();
                kcp.Send(buffer.Memory.Span.Slice(0, lenght));
                buffer.Dispose();
            }
            else
            {
                var (buffer, lenght) = SendWriter.Pop();
                buffer.Dispose();
            }
        }

        ///接收===================================================================

        protected Writer kcprecv = new Writer(BufferSizer);
        async void KCPRecv()
        {
            while (true)
            {
                await kcp.Recv(kcprecv).ConfigureAwait(false);
                var (buffer, lenght) = kcprecv.Pop();
                if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
                {
                    ProcessBody(new ReadOnlySequence<byte>(segment.Array, 0, lenght));
                }
                buffer.Dispose();
            }
        }

        protected override void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer, int start, int count)
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
                case UdpRemoteMessageDefine.LLMsg:
                    ///Test消息不通过Kcp处理
                    RecvLLMsg(recvbuffer, start + 1, count - 1);
                    break;
                case UdpRemoteMessageDefine.Common:
                    RecvPureBuffer(recvbuffer, start + 1, count - 1);
                    break;
                default:
                    break;
            }
        }

        public void RecvLLMsg(byte[] buffer, int start, int count)
        {
            ProcessBody(new ReadOnlySequence<byte>(buffer, start, count));
        }

        protected override void RecvPureBuffer(byte[] buffer, int start, int count)
        {
            kcp.Input(new ReadOnlySpan<byte>(buffer, start, count));
        }

        protected override void RecvPureBuffer(ReadOnlySequence<byte> sequence)
        {
            kcp.Input(sequence);
        }

        public override Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            InitKcp(1001);
            return base.ConnectAsync(endPoint, retryCount);
        }
    }
}
