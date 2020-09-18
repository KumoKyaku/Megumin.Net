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

        /// <summary>
        /// 正在连接的
        /// </summary>
        readonly Dictionary<IPEndPoint, UdpRemote> kcpPool = new Dictionary<IPEndPoint, UdpRemote>();
        readonly Dictionary<int, UdpRemote> kcpPool2 = new Dictionary<int, UdpRemote>();

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
                    if (!kcpPool.TryGetValue(res.RemoteEndPoint, out var remote))
                    {
                        var newkcpR = await CreateKcpRemote(res);
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
        /// kcp连接过程
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        Task<KcpRemote> CreateKcpRemote(UdpReceiveResult res)
        {
            KcpRemote remote = new KcpRemote();
            remote.ID = InterlockedID<KcpRemote>.NewID();
            Span<byte> span = new byte[10];
            return default;
        }
    }
}
