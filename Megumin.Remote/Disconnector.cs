using Net.Remote;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public partial class TcpRemote
    {
        /// <summary>
        /// 当前Socket可不可以发送
        /// </summary>
        bool canSend = true;
        /// <summary>
        /// 能不能继续放入发送队列
        /// </summary>
        bool canEnqueuSQ = true;
        bool remoteShutDownSend = false;

        public Disconnector disconnector = new Disconnector();

        /// <summary>
        /// 断开器
        /// </summary>
        public class Disconnector: IDisconnectable
        {
            readonly object innerlock = new object();
            public bool IsDising = false;
            public TcpRemote tcpRemote;

            /// <summary>
            /// 发送或接收出现错误
            /// </summary>
            /// <param name="error"></param>
            public void OnEx(SocketError error)
            {
                if (IsDising)
                {
                    //正在断开就忽略
                    return;
                }

                IsDising = true;

                tcpRemote.canEnqueuSQ = false;
                tcpRemote.canSend = false;
                tcpRemote.Client.Shutdown(SocketShutdown.Send);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="triggerOnDisConnect"></param>
            /// <param name="waitSendQueue"></param>
            public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
            {
                //todo 进入断开流程，不允许外部继续Send


                if (waitSendQueue)
                {
                    //todo 等待当前发送缓冲区发送结束。
                }

                //进入清理阶段
                tcpRemote.StopWork();

                if (triggerOnDisConnect)
                {
                    //触发回调
                    tcpRemote.OnDisconnect(SocketError.SocketError, ActiveOrPassive.Active);
                    tcpRemote.PostDisconnect(SocketError.SocketError, ActiveOrPassive.Active);
                }

                tcpRemote.IsVaild = false;
            }

            /// <summary>
            /// 收到0字节 表示远程主动断开连接
            /// </summary>
            internal async void OnRecv0()
            {
                lock (innerlock)
                {
                    if (IsDising)
                    {
                        //正在断开就忽略
                        return;
                    }

                    IsDising = true;
                }

                //收到0字节 表示远程主动断开连接
                tcpRemote.MWorkState = RWorkState.Stoping;
                //停止发送。
                tcpRemote.Client.Shutdown(SocketShutdown.Send);

                tcpRemote.pipe.Writer.Complete();
                //等待已接受缓存处理完毕
                //await tcpRemote.EndDealRecv();

                tcpRemote.MWorkState = RWorkState.Stoped;
                //触发回调
                tcpRemote.OnDisconnect(SocketError.SocketError, ActiveOrPassive.Active);
                tcpRemote.PostDisconnect(SocketError.SocketError, ActiveOrPassive.Active);
            }

            internal void OnSendError(SocketException e)
            {
                throw new NotImplementedException();
            }
            internal void OnSendError(SocketError e)
            {
                throw new NotImplementedException();
            }
        }

    }
}
