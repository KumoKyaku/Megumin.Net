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
                    //todo add listenUdpclient.
                    var sendSocket = SendSockets[connected.Count % SendSockets.Length];
                    udp.SetSocket(sendSocket);
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
