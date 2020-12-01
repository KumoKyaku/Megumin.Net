using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Net.Remote;

namespace Megumin.Remote
{
    public class KcpRemoteListener : UdpRemoteListener
    {
        public KcpRemoteListener(int port, AddressFamily addressFamily = AddressFamily.InterNetworkV6)
            : base(port, addressFamily)
        {
        }

        protected override UdpRemote CreateNew(IPEndPoint endPoint, UdpAuthResponse answer)
        {
            KcpRemote remote = CreateFunc?.Invoke() as KcpRemote;
            if (remote == null)
            {
                remote = new KcpRemote();
            }
            remote.InitKcp(answer.KcpChannel);
            remote.IsVaild = true;
            remote.ConnectIPEndPoint = endPoint;
            remote.GUID = answer.Guid;
            remote.Password = answer.Password;
            lut.Add(remote.GUID, remote);
            connected.Add(endPoint, remote);
            return remote;
        }
    }
}
