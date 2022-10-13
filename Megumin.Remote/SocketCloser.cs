using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using static Megumin.Remote.TcpRemote;

namespace Megumin.Remote
{
    public interface IDisconnecHandle
    {
        /// <summary>
        /// 当网络连接已经断开, 发送和接受可能有一个没有完全停止。
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// <param name="error"></param>
        /// <param name="options"></param>
        /// <remarks>主要用于通知外部停止继续发送</remarks>
        void PreDisconnect(SocketError error, object options = null);

        /// <summary>
        /// 断开连接之后
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="options"></param>
        /// <remarks>可以用于触发重连，并将现有发送缓冲区转移到心得连接中</remarks>
        void OnDisconnect(SocketError error, object options = null);

        /// <summary>
        /// 断开连接之后
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="options"></param>
        void PostDisconnect(SocketError error, object options = null);
    }

    /// <summary>
    /// 安全关闭一个socket很麻烦，根本搞不清楚调用那个函数会抛出异常。
    /// </summary>
    public class SocketCloser
    {
        public TraceListener TraceListener { get; set; }
        readonly object innerlock = new object();
        public bool IsDisconnecting { get; internal protected set; } = false;
        public void SafeClose(Socket socket,
                              SocketError error,
                              IDisconnecHandle tcpRemote,
                              bool triggerDisConnectHandle = false,
                              bool waitSendQueue = false,
                              object options = null)
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

            try
            {
                if (triggerDisConnectHandle)
                {
                    tcpRemote.PreDisconnect(error, options);
                }
                //停止收发。
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                //忽略  
                TraceListener?.WriteLine(e);
            }
            finally
            {
                try
                {
                    //等待已接受缓存处理完毕
                    //await tcpRemote.EndDealRecv();

                    //关闭接收，这个过程中可能调用本身出现异常。
                    //也可能导致异步接收部分抛出，由于disconnectSignal只能使用一次，所有这个阶段异常都会被忽略。
                    socket.Disconnect(false);
                }
                catch (Exception e)
                {
                    //忽略  
                    TraceListener?.WriteLine(e);
                }
                finally
                {
                    try
                    {
                        socket.Close();
                        if (triggerDisConnectHandle)
                        {
                            //触发回调
                            tcpRemote.OnDisconnect(error, options);
                        }
                    }
                    catch (Exception e)
                    {
                        //忽略  
                        TraceListener?.WriteLine(e.ToString());
                    }
                    finally
                    {
                        if (triggerDisConnectHandle)
                        {
                            tcpRemote.PostDisconnect(error, options);
                        }
                    }
                }
            }
        }

        public void OnRecv0(Socket client, IDisconnecHandle udpRemote)
        {
            SafeClose(client, SocketError.Shutdown, udpRemote, true);
        }
    }

    public class DisconnectOptions
    {
        public ActiveOrPassive ActiveOrPassive { get; set; } = ActiveOrPassive.Active;
    }
}
