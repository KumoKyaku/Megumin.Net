//using System;
//using System.Collections.Generic;
//using System.Net.Sockets.Kcp;
//using System.Text;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
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

        public void InitKcp(int kcpChannel)
        {
            if (kcp == null)
            {
                kcp = new FakeKcpIO();
                KcpOutput();
                KCPRecv();
            }
        }

        // 发送===================================================================
        protected Writer kcpout = new Writer(65535);
        async void KcpOutput()
        {
            while (true)
            {
                await kcp.Output(kcpout);
                var (buffer, lenght) = kcpout.Pop();
                var sendbuffer = MemoryPool<byte>.Shared.Rent(lenght + 1);
                sendbuffer.Memory.Span[0] = UdpRemoteMessageDefine.Common;
                buffer.Memory.Span.Slice(0, lenght).CopyTo(sendbuffer.Memory.Span.Slice(1));
                SocketSend(sendbuffer, lenght + 1);
            }
        }

        protected override void Send(int rpcID, object message, object options = null)
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

        async void KCPRecv()
        {
            while (true)
            {
                var res = await kcp.Recv();
                ProcessBody(res);
            }
        }

        protected internal override void ServerSideRecv(IPEndPoint endPoint, byte[] buffer, int offset, int count)
        {
            ConnectIPEndPoint = endPoint;
            kcp.Input(new ReadOnlySpan<byte>(buffer, offset + 1, count));
        }

        protected override void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer)
        {
            byte messageType = recvbuffer[0];
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
                    ProcessBody(new ReadOnlySequence<byte>(recvbuffer, 1, recvbuffer.Length - 1));
                    break;
                case UdpRemoteMessageDefine.Common:
                    kcp.Input(new ReadOnlySpan<byte>(recvbuffer, 1, recvbuffer.Length));
                    break;
                default:
                    break;
            }
        } 
    }
}
