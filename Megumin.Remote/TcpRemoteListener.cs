using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public class TcpRemoteListener
    {
        private TcpListener tcpListener;
        public IPEndPoint ConnectIPEndPoint { get; set; }

        public TcpRemoteListener(int port)
        {
            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None, port);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task<Socket> Accept()
        {
            if (tcpListener == null)
            {
                ///同时支持IPv4和IPv6
                tcpListener = TcpListener.Create(ConnectIPEndPoint.Port);

                tcpListener.AllowNatTraversal(true);
            }

            tcpListener.Start();
            try
            {
                ///此处有远程连接拒绝异常
                return tcpListener.AcceptSocketAsync();
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e);
                ///出现异常重新开始监听
                tcpListener = null;
                return Accept();
            }
        }

        /// <summary>
        /// 创建TCPRemote并开始接收
        /// </summary>
        /// <returns></returns>
        public async ValueTask<T> ListenAsync<T>(Func<T> createFunc)
            where T : TcpRemote
        {
            var remoteSocket = await Accept();
            var remote = createFunc.Invoke();
            remote.SetSocket(remoteSocket);

            //异步启动remote，防止阻塞监听
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            Task.Run(
                () =>
                {
                    remote.StartWork();
                });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            return remote;
        }

        public void Stop() => tcpListener?.Stop();
    }
}
