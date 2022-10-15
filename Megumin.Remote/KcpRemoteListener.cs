using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Net.Remote;

namespace Megumin.Remote
{
    /// <summary>
    /// Kcp测试10000连接没有成功。5000也不性。推测应该是UdpListenner一个端口无法处理这么大流量，大量丢包。
    /// 500个Listener还是有错误发生 deadlink。有的kcp发生断联。
    /// 其实和连接多少没关系，还是数据量大小的问题。一个UdpRemoteListener不应该处理过多的连接。
    /// 300个比较合适。
    /// 工程实践中使用多个端口负载均衡比较好。
    /// <para></para>
    /// 测试发现，当发生打嗝卡顿是，rto迅速增大。启用新的连接rto也会直接增大。
    /// 所以问题出在接收侧，接收端口无法处理过大的数据量。
    /// 但是用UDP测试，不能复现这种一个UdpRemoteListener不应该处理过多的连接的情况。尽管丢包现象明显。
    /// </summary>
    [Obsolete("", true)]
    public class KcpRemoteListenerOld : UdpRemoteListenerOld/*, IListenerOld<KcpRemote>*/
    {
        public KcpRemoteListenerOld(int port)
            : base(port)
        {
        }

        public KcpRemoteListenerOld(int port, AddressFamily addressFamily)
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

        //ValueTask<R> IListenerOld<KcpRemote>.ListenAsync<R>(Func<R> createFunc)
        //{
        //    return ListenAsync(createFunc);
        //}
    }

    /// <summary>
    /// <inheritdoc cref="KcpRemoteListenerOld"/>
    /// <para>--------</para>
    /// 新的UdpRemoteListener优化了接收循环，勉强能处理3000个连接。打嗝假死现象减弱了。
    /// </summary>
    public class KcpRemoteListener : UdpRemoteListener/*, IListener<KcpRemote>*/
    {
        public KcpRemoteListener(int port, AddressFamily? addressFamily = null) : base(port, addressFamily)
        {
        }

        //public new ValueTask<R> ReadAsync<R>(Func<R> createFunc) where R : KcpRemote
        //{
        //    return base.ReadAsync(createFunc);
        //}

        public ValueTask ReadAsync(KcpRemote trans)
        {
            return base.ReadAsync(trans);
        }
    }
}
