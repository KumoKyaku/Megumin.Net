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
        internal protected Disconnector disconnector = new Disconnector();

        /// <summary>
        /// 断开器
        /// file:///E:\Git\Megumin.Net\Doc\如何正确处理网络断开.md
        /// </summary>
        internal protected class Disconnector: IDisconnectable
        {
            readonly object innerlock = new object();
            public bool IsDisconnecting = false;
            public TcpRemote tcpRemote;

            /// <summary>
            /// 收到0字节 表示远程主动断开连接
            /// </summary>
            internal void OnRecv0()
            {
                lock (innerlock)
                {
                    if (IsDisconnecting)
                    {
                        //正在断开就忽略
                        return;
                    }

                    IsDisconnecting = true;
                }

                tcpRemote.OnDisconnect(SocketError.Shutdown, ActiveOrPassive.Passive);
                tcpRemote.RemoteState = WorkState.StopingAll;

                //停止发送。
                tcpRemote.Client.Shutdown(SocketShutdown.Send);

                tcpRemote.Pipe.Writer.Complete();
                //等待已接受缓存处理完毕
                //await tcpRemote.EndDealRecv();

                //关闭接收，这个过程中可能调用本身出现异常。
                //也可能导致异步接收部分抛出，由于disconnectSignal只能使用一次，所有这个阶段异常都会被忽略。
                try
                {
                    tcpRemote.Client.Shutdown(SocketShutdown.Both);
                    tcpRemote.Client.Disconnect(false);
                }
                finally
                {
                    tcpRemote.Client.Close();
                }

                //触发回调
                tcpRemote.RemoteState = WorkState.Stoped;
                tcpRemote.PostDisconnect(SocketError.Shutdown, ActiveOrPassive.Passive);
            }

            /// <summary>
            /// 接收出现错误
            /// </summary>
            /// <param name="error"></param>
            internal void OnRecvError(SocketError error)
            {
                lock (innerlock)
                {
                    if (IsDisconnecting)
                    {
                        //正在断开就忽略
                        return;
                    }

                    IsDisconnecting = true;
                }

                tcpRemote.OnDisconnect(error, ActiveOrPassive.Passive);
                tcpRemote.RemoteState = WorkState.StopingAll;

                //停止发送。
                tcpRemote.Client.Shutdown(SocketShutdown.Send);

                tcpRemote.Pipe.Writer.Complete();
                //等待已接受缓存处理完毕
                //await tcpRemote.EndDealRecv();

                //关闭接收，这个过程中可能调用本身出现异常。
                //也可能导致异步接收部分抛出，由于disconnectSignal只能使用一次，所有这个阶段异常都会被忽略。
                try
                {
                    tcpRemote.Client.Shutdown(SocketShutdown.Both);
                    tcpRemote.Client.Disconnect(false);
                }
                finally
                {
                    tcpRemote.Client.Close();
                }

                //触发回调
                tcpRemote.RemoteState = WorkState.Stoped;
                tcpRemote.PostDisconnect(error, ActiveOrPassive.Passive);
            }

            /// <summary>
            /// 发送出现错误
            /// </summary>
            /// <param name="error"></param>
            internal void OnSendError(SocketError error)
            {
                lock (innerlock)
                {
                    if (IsDisconnecting)
                    {
                        //正在断开就忽略
                        return;
                    }

                    IsDisconnecting = true;
                }

                tcpRemote.OnDisconnect(error, ActiveOrPassive.Passive);
                tcpRemote.RemoteState = WorkState.StopingAll;

                //停止发送。
                tcpRemote.Client.Shutdown(SocketShutdown.Send);

                //关闭接收，这个过程中可能调用本身出现异常。
                //也可能导致异步接收部分抛出，由于disconnectSignal只能使用一次，所有这个阶段异常都会被忽略。
                try
                {
                    tcpRemote.Client.Shutdown(SocketShutdown.Both);
                    tcpRemote.Client.Disconnect(false);
                }
                finally
                {
                    tcpRemote.Client.Close();
                }

                tcpRemote.Pipe.Writer.Complete();
                //等待已接受缓存处理完毕
                //await tcpRemote.EndDealRecv();

                //触发回调
                tcpRemote.RemoteState = WorkState.Stoped;
                tcpRemote.PostDisconnect(error, ActiveOrPassive.Passive);
            }

            public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
            {
                lock (innerlock)
                {
                    if (IsDisconnecting)
                    {
                        //正在断开就忽略
                        return;
                    }

                    IsDisconnecting = true;
                }

                //进入断开流程，不允许外部继续Send
                if (triggerOnDisConnect)
                {
                    tcpRemote.OnDisconnect(SocketError.Disconnecting, ActiveOrPassive.Active);
                }
                tcpRemote.RemoteState = WorkState.StopingWaitQueueSending;

                if (waitSendQueue)
                {
                    //todo 等待当前发送缓冲区发送结束。
                }

                tcpRemote.RemoteState = WorkState.StopingAll;

                //关闭接收，这个过程中可能调用本身出现异常。
                try
                {
                    tcpRemote.Client.Shutdown(SocketShutdown.Both);
                    tcpRemote.Client.Disconnect(false);
                }
                finally
                {
                    tcpRemote.Client.Close();
                }

                tcpRemote.Pipe.Writer.Complete();
                //todo 等待已接受缓存处理完毕
                //await tcpRemote.EndDealRecv();

                tcpRemote.RemoteState = WorkState.Stoped;
                if (triggerOnDisConnect)
                {
                    //触发回调
                    tcpRemote.PostDisconnect(SocketError.Disconnecting, ActiveOrPassive.Active);
                }
            }

        }

    }
}
