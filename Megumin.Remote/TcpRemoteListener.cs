﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Net.Remote;

namespace Megumin.Remote
{
    [Obsolete("", true)]
    public class TcpRemoteListenerOld /*: IListenerOld<TcpRemote>*/
    {
        private TcpListener tcpListener;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public System.Diagnostics.TraceListener TraceListener { get; set; }

        public TcpRemoteListenerOld(int port)
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

                //tcpListener.AllowNatTraversal(true);
            }

            tcpListener.Start();
            try
            {
                ///此处有远程连接拒绝异常
                return tcpListener.AcceptSocketAsync();
            }
            catch (InvalidOperationException e)
            {
                TraceListener?.WriteLine(e.ToString());
                ///出现异常重新开始监听
                tcpListener = null;
                return Accept();
            }
            catch (Exception e)
            {
                TraceListener?.WriteLine(e);
                return Task.FromResult<Socket>(null);
            }
        }

        /// <summary>
        /// 创建TCPRemote并开始接收
        /// </summary>
        /// <returns></returns>
        public async ValueTask<R> ListenAsync<R>(Func<R> createFunc)
            where R : TcpTransport
        {
            Socket remoteSocket = null;
            try
            {
                remoteSocket = await Accept();
            }
            catch (Exception)
            {

            }

            if (remoteSocket != null)
            {
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

            return null;
        }

        public void Stop() => tcpListener?.Stop();


    }

    public class TcpRemoteListener : IListener
    {
        private TcpListener tcpListener;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public TraceListener TraceListener { get; set; }
        public TcpRemoteListener(int port)
        {
            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None, port);
        }

        public QueuePipe<Socket> AcceptedSockets { get; protected set; } = new QueuePipe<Socket>();
        public void Start(object option = null)
        {
            if (tcpListener == null)
            {
                ///同时支持IPv4和IPv6
                tcpListener = TcpListener.Create(ConnectIPEndPoint.Port);
                tcpListener.Start();
                //tcpListener.AllowNatTraversal(true);
                Accept();
            }
            else
            {
                return;
            }
        }

        public void Stop()
        {
            tcpListener?.Stop();
            tcpListener = null;
        }

        public async void Accept()
        {
            try
            {
                while (true)
                {
                    ///此处有远程连接拒绝异常
                    var socket = await tcpListener.AcceptSocketAsync().ConfigureAwait(false);

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                    Task.Run(() => { AcceptedSockets.Write(socket); });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                }
            }
            catch (ObjectDisposedException)
            {
                //正常Stop触发
            }
            catch (Exception e)
            {
                OnAcceptException(e);
                return;
            }
        }

        //public async ValueTask<R> ReadAsync<R>(Func<R> createFunc)
        //    where R : TcpRemote
        //{
        //    var socket = await AcceptedSockets.ReadAsync().ConfigureAwait(false);
        //    var remote = createFunc.Invoke();
        //    remote.SetSocket(socket);
        //    remote.StartWork();
        //    return remote;
        //}

        protected virtual void OnAcceptException(Exception e)
        {
            TraceListener?.WriteLine(e.ToString());
            tcpListener = null;
        }

        public async ValueTask ReadAsync(TcpTransport transport)
        {
            if (tcpListener == null)
            {
                TraceListener?.Fail("TcpListener is null.");
            }
            var socket = await AcceptedSockets.ReadAsync().ConfigureAwait(false);
            transport.SetSocket(socket);
            transport.StartWork();
        }

        public ValueTask ReadAsync(IRemote remote)
        {
            if (remote.Transport is TcpTransport transport)
            {
                return ReadAsync(transport);
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }
}
