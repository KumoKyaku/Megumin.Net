using Net.Remote;
using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace Megumin.Remote
{
    /// <summary>
    /// 安全关闭一个socket很麻烦，根本搞不清楚调用那个函数会抛出异常。
    /// </summary>
    public class SocketCloser
    {
        readonly object innerlock = new object();
        public bool IsDisconnecting { get; internal protected set; } = false;
        public void SafeClose(Socket socket,
                              SocketError error,
                              IDisconnectHandler handler,
                              bool triggerDisConnectHandle = false,
                              bool waitSendQueue = false,
                              object options = null,
                              TraceListener traceListener = null)
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
                    handler?.PreDisconnect(error, options);
                }
                //停止收发。
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                //忽略  
                traceListener?.WriteLine(e);
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
                    traceListener?.WriteLine(e);
                }
                finally
                {
                    try
                    {
                        socket.Close();
                        if (triggerDisConnectHandle)
                        {
                            //触发回调
                            handler?.OnDisconnect(error, options);
                        }
                    }
                    catch (Exception e)
                    {
                        //忽略  
                        traceListener?.WriteLine(e.ToString());
                    }
                    finally
                    {
                        if (triggerDisConnectHandle)
                        {
                            handler?.PostDisconnect(error, options);
                        }
                    }
                }
            }
        }

        public void OnRecv0(Socket client, IDisconnectHandler handler, TraceListener traceListener = null)
        {
            SafeClose(client, SocketError.Shutdown, handler, true, traceListener: traceListener);
        }
    }

    public class DisconnectOptions
    {
        public ActiveOrPassive ActiveOrPassive { get; set; } = ActiveOrPassive.Active;
    }
}
