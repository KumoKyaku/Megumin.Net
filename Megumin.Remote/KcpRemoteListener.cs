using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Net.Remote;

namespace Megumin.Remote
{
    public class KcpRemoteListener : UdpRemoteListener, IListener<KcpRemote>
    {
        public KcpRemoteListener(int port)
            : base(port)
        {
        }

        public KcpRemoteListener(int port, AddressFamily addressFamily)
            : base(port, addressFamily)
        {
        }

        protected override async void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer)
        {
            byte messageType = recvbuffer[0];
            switch (messageType)
            {
                case UdpRemoteMessageDefine.UdpAuthRequest:
                    //被动侧不处理主动侧提出的验证。
                    break;
                case UdpRemoteMessageDefine.UdpAuthResponse:
                    authHelper.DealAnswerBuffer(endPoint, recvbuffer);
                    break;
                case UdpRemoteMessageDefine.LLData:
                    {
                        //不通过Kcp协议处理
                        var remote = await FindRemote(endPoint).ConfigureAwait(false);
                        if (remote is KcpRemote kcpRemote)
                        {
                            kcpRemote.RecvLLMsg(recvbuffer, 1, recvbuffer.Length - 1);
                        }
                    }
                    break;
                case UdpRemoteMessageDefine.UdpData:
                    {
                        var remote = await FindRemote(endPoint).ConfigureAwait(false);
                        if (remote != null)
                        {
                            remote.ServerSideRecv(endPoint, recvbuffer, 0, recvbuffer.Length);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        protected override UdpRemote CreateNew(IPEndPoint endPoint, UdpAuthResponse answer)
        {
            if (remoteCreators.TryDequeue(out var cre))
            {
                var (continueAction, udp) = cre.Invoke();

                KcpRemote remote = udp as KcpRemote;
                if (remote != null)
                {
                    remote.InitKcp(answer.KcpChannel);
                    remote.IsVaild = true;
                    remote.ConnectIPEndPoint = endPoint;
                    remote.GUID = answer.Guid;
                    remote.Password = answer.Password;
                    udp.Client = SendSockets[connected.Count % SendSockets.Length];
                    lut.Add(answer.Guid, remote);
                    connected.Add(endPoint, remote);
                }

                continueAction?.Invoke();
                return remote;
            }

            return null;
        }

        ValueTask<R> IListener<KcpRemote>.ListenAsync<R>(Func<R> createFunc)
        {
            return ListenAsync(createFunc);
        }
    }
}
