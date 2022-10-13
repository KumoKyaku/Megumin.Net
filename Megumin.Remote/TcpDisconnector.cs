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
        internal protected Disconnector disconnector;

        /// <summary>
        /// 断开器
        /// file:///W:\Git\Megumin.Net\Doc\如何正确处理网络断开.md
        /// https://stackoverflow.com/questions/35229143/what-exactly-do-sockets-shutdown-disconnect-close-and-dispose-do
        /// </summary>
        internal protected class Disconnector : IDisconnectable
        {
            readonly object innerlock = new object();
            /// <summary>
            /// 设置为true后，不需要重置为false，因为设计上一个remote只能触发断开一次。
            /// </summary>
            public bool IsDisconnecting = false;
            public TcpRemote tcpRemote;

            protected void OnSocketError(SocketError error)
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

                SocketShutdownDisconnectClose(error, ActiveOrPassive.Passive);
            }

            protected void SocketShutdownDisconnectClose(SocketError error, ActiveOrPassive aop)
            {
                try
                {
                    tcpRemote.PreDisconnect(error, null);
                    tcpRemote.RemoteState = WorkState.StopingAll;
                    //停止收发。
                    tcpRemote.Client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e)
                {
                    //忽略  
                    tcpRemote.TraceListener?.WriteLine(e.ToString());
                }
                finally
                {
                    try
                    {
                        //等待已接受缓存处理完毕
                        //await tcpRemote.EndDealRecv();

                        //关闭接收，这个过程中可能调用本身出现异常。
                        //也可能导致异步接收部分抛出，由于disconnectSignal只能使用一次，所有这个阶段异常都会被忽略。
                        tcpRemote.Client.Disconnect(false);
                    }
                    catch (Exception e)
                    {
                        //忽略  
                        tcpRemote.TraceListener?.WriteLine(e.ToString());
                    }
                    finally
                    {
                        try
                        {
                            tcpRemote.RecvPipe.Writer.Complete();
                            tcpRemote.Client.Close();
                            //触发回调
                            tcpRemote.RemoteState = WorkState.Stoped;
                            tcpRemote.OnDisconnect(error, null);
                        }
                        catch (Exception e)
                        {
                            //忽略  
                            tcpRemote.TraceListener?.WriteLine(e.ToString());
                        }
                        finally
                        {
                            tcpRemote.PostDisconnect(error, null);
                        }
                    }
                }
            }

            /// <summary>
            /// 收到0字节 表示远程主动断开连接
            /// </summary>
            internal void OnRecv0()
            {
                //这里用Shutdown，表示远端发起断开。 Disconnecting用来表示本地主动发起断开。
                OnRecvError(SocketError.Shutdown);
            }

            /// <summary>
            /// 接收出现错误
            /// </summary>
            /// <param name="error"></param>
            internal void OnRecvError(SocketError error)
            {
                OnSocketError(error);
            }

            /// <summary>
            /// 发送出现错误
            /// </summary>
            /// <param name="error"></param>
            internal void OnSendError(SocketError error)
            {
                OnSocketError(error);
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
                var options = new DisconnectOptions() { ActiveOrPassive = ActiveOrPassive.Active };
                try
                {
                    //进入断开流程，不允许外部继续Send
                    if (triggerOnDisConnect)
                    {
                        tcpRemote.PreDisconnect(SocketError.Disconnecting, options);
                    }
                    tcpRemote.RemoteState = WorkState.StopingWaitQueueSending;

                    if (waitSendQueue)
                    {
                        //todo 等待当前发送缓冲区发送结束。
                    }

                    tcpRemote.RemoteState = WorkState.StopingAll;

                    //关闭接收，这个过程中可能调用本身出现异常。
                    tcpRemote.Client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e)
                {
                    //忽略  
                    tcpRemote.TraceListener?.WriteLine(e.ToString());
                }
                finally
                {
                    try
                    {
                        tcpRemote.Client.Disconnect(false);
                    }
                    catch (Exception e)
                    {
                        //忽略  
                        tcpRemote.TraceListener?.WriteLine(e.ToString());
                    }
                    finally
                    {
                        try
                        {
                            tcpRemote.RecvPipe.Writer.Complete();
                            //todo 等待已接受缓存处理完毕
                            //await tcpRemote.EndDealRecv();
                            tcpRemote.Client.Close();
                            tcpRemote.RemoteState = WorkState.Stoped;
                            if (triggerOnDisConnect)
                            {
                                //触发回调
                                tcpRemote.OnDisconnect(SocketError.Disconnecting, options);
                            }
                        }
                        catch (Exception e)
                        {
                            //忽略  
                            tcpRemote.TraceListener?.WriteLine(e.ToString());
                        }
                        finally
                        {
                            if (triggerOnDisConnect)
                            {
                                //触发回调
                                tcpRemote.PostDisconnect(SocketError.Shutdown, options);
                            }
                        }
                    }
                }
            }
        }
    }
}
