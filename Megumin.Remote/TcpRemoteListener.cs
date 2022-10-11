using Net.Remote;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{

    public class TcpRemoteListener : IListener<TcpRemote>
    {
        private TcpListener tcpListener;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public System.Diagnostics.TraceListener TraceListener { get; set; }

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
            where R : TcpRemote
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

    public class TcpRemoteListener2 : IListener2<TcpRemote>
    {
        /// <summary>
        /// <inheritdoc cref="IPipe{T}"/>
        /// <para></para>这是个简单的实现,更复杂使用微软官方实现<see cref="System.Threading.Channels.Channel.CreateBounded{T}(int)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal protected class QueuePipe<T> : Queue<T>
        {
            readonly object _innerLock = new object();
            private TaskCompletionSource<T> source;

            //线程同步上下文由Task机制保证，无需额外处理
            //SynchronizationContext callbackContext;
            //public bool UseSynchronizationContext { get; set; } = true;

            public virtual void Write(T item)
            {
                lock (_innerLock)
                {
                    if (source == null)
                    {
                        Enqueue(item);
                    }
                    else
                    {
                        if (Count > 0)
                        {
                            throw new Exception("内部顺序错误，不应该出现，请联系作者");
                        }

                        var next = source;
                        source = null;
                        next.TrySetResult(item);
                    }
                }
            }

            public new void Enqueue(T item)
            {
                lock (_innerLock)
                {
                    base.Enqueue(item);
                }
            }

            public void Flush()
            {
                lock (_innerLock)
                {
                    if (Count > 0)
                    {
                        var res = Dequeue();
                        var next = source;
                        source = null;
                        next?.TrySetResult(res);
                    }
                }
            }

            public virtual Task<T> ReadAsync()
            {
                lock (_innerLock)
                {
                    if (this.Count > 0)
                    {
                        var next = Dequeue();
                        return Task.FromResult(next);
                    }
                    else
                    {
                        source = new TaskCompletionSource<T>();
                        return source.Task;
                    }
                }
            }

            public ValueTask<T> ReadValueTaskAsync()
            {
                throw new NotImplementedException();
            }
        }

        private TcpListener tcpListener;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public TraceListener TraceListener { get; set; }
        public TcpRemoteListener2(int port)
        {
            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None, port);
        }

        QueuePipe<Socket> sockets = new QueuePipe<Socket>();
        public void Start(object option = null)
        {
            if (tcpListener == null)
            {
                ///同时支持IPv4和IPv6
                tcpListener = TcpListener.Create(ConnectIPEndPoint.Port);
            }
            else
            {
                return;
            }

            tcpListener.Start();
            //tcpListener.AllowNatTraversal(true);
            Accept();
        }

        public void Stop()
        {
            tcpListener?.Stop();
        }

        public async void Accept()
        {
            try
            {
                ///此处有远程连接拒绝异常
                var socket = await tcpListener.AcceptSocketAsync();
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                Task.Run(() => { sockets.Write(socket); });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                Accept();
            }
            catch (Exception e)
            {
                OnAcceptException(e);
                return;
            }
        }

        public async ValueTask<R> ReadAsync<R>(Func<R> createFunc)
            where R : TcpRemote
        {
            var socket = await sockets.ReadAsync();
            var remote = createFunc.Invoke();
            remote.SetSocket(socket);
            remote.StartWork();
            return remote;
        }

        protected virtual void OnAcceptException(Exception e)
        {
            TraceListener?.WriteLine(e.ToString());
            tcpListener = null;
        }
    }
}
