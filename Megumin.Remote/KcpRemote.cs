﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Megumin.Remote
{

    /// <summary>
    /// 测试发送消息 1000个 1024*10消息，多次发送，有几率假死。
    /// debug发现发送的Kcpdata和接收的Kcpdata对不上。发送一个data，接收到的是旧的数据。
    /// 新的数据不知道去了那里。感觉像卡在系统的接收数据缓冲里。可能是UDP底层出了问题。
    /// 有可能是数据量过大IOCP出现了假死，UdpClient.ReceiveAsync异步触发出了问题。
    /// 实在找不到问题所在。暂时搁置。
    /// 目前处理方法是使用发送心跳包方式保活。收不到直接就进入断线流程。
    /// <para></para>
    /// 工程实践中由于消息是不间断的，总是出现打嗝卡顿，后续消息会触发fastack。假死现象要比测试少一点。
    /// </summary>
    public partial class KcpRemote : UdpRemote
    {
        public PoolSegManager.KcpIO KcpCore { get; internal protected set; } = null;
        IKcpUpdate kcpUpdate = null;
        const int BufferSizer = 1024 * 4;

        public KcpRemote(AddressFamily? addressFamily = null) : base(addressFamily)
        {
        }

        public void InitKcp(int kcpChannel)
        {
            if (KcpCore == null)
            {
                KcpIOChannel = kcpChannel;
                var kcpIO = new PoolSegManager.KcpIO((uint)KcpIOChannel);

                //具体设置参数要根据项目调整。测试数据量一大有打嗝和假死现象。还没搞清楚原因。

                kcpIO.NoDelay(2, 5, 2, 1);
                kcpIO.WndSize(64, 128);
                ///不要限制最大fastack，rto快速增长时很难恢复，指望快速重传来减小延迟。
                kcpIO.fastlimit = -1;

                KcpCore = kcpIO;
                kcpUpdate = kcpIO;

                lock (kcpUpdateLock)
                {
                    AllKcp.Add(kcpUpdate);
                }

                KCPUpdate();
                KcpOutput();
                KCPRecv();
            }
        }

        internal protected virtual void SafeCloseKcpCore()
        {
            Task.Run(() =>
            {
                lock (kcpUpdateLock)
                {
                    AllKcp.Remove(kcpUpdate);
                    if (kcpUpdate is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            });
        }

        static readonly Random convRandom = new Random();
        public override Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            InitKcp(convRandom.Next(1000, 10000));
            return base.ConnectAsync(endPoint, retryCount);
        }

        protected override async ValueTask ConnectSideShutdown(bool triggerOnDisConnect, bool waitSendQueue)
        {
            await base.ConnectSideShutdown(triggerOnDisConnect, waitSendQueue);
            SafeCloseKcpCore();
        }

        protected override void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {
            base.OnDisconnect(error, activeOrPassive);
            TraceListener?.WriteLine($"连接断开");
        }

        public override void PreDisconnect(SocketError error, object options = null)
        {
            base.PreDisconnect(error, options);
            SafeCloseKcpCore();
        }
    }

    public partial class KcpRemote
    {
        //循环Tick================================================================
        internal protected static readonly List<IKcpUpdate> AllKcp = new List<IKcpUpdate>();
        internal protected static readonly List<IKcpUpdate> DisposedKcp = new List<IKcpUpdate>();
        static bool IsGlobalUpdate = false;
        static readonly object kcpUpdateLock = new object();
        protected async void KCPUpdate()
        {
            lock (kcpUpdateLock)
            {
                if (IsGlobalUpdate)
                {
                    return;
                }
                IsGlobalUpdate = true;
            }

            try
            {
                DateTimeOffset last = DateTimeOffset.UtcNow;
                while (true)
                {
                    var time = DateTimeOffset.UtcNow;
                    if ((time - last).TotalMilliseconds < 1)
                    {
                        await Task.Delay(1);
                        //await Task.Yield();  //会吃满所有CPU？
                    }
                    else
                    {
                        last = time;
                        if (AllKcp.Count == 0)
                        {
                            break;
                        }

                        lock (kcpUpdateLock)
                        {
                            foreach (var item in AllKcp)
                            {
                                try
                                {
                                    item?.Update(time);
                                }
                                catch (ObjectDisposedException)
                                {
                                    DisposedKcp.Add(item);
                                }
                            }

                            foreach (var item in DisposedKcp)
                            {
                                if (item != null)
                                {
                                    AllKcp.Remove(item);
                                }
                            }
                            DisposedKcp.Clear();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TraceListener?.WriteLine(e.ToString());
            }
            finally
            {
                IsGlobalUpdate = false;
            }
        }
    }

    public partial class KcpRemote
    {
        // 发送===================================================================
        protected UdpBufferWriter kcpout = new UdpBufferWriter(BufferSizer);
        async void KcpOutput()
        {
            while (true)
            {
                kcpout.WriteHeader(UdpRemoteMessageDefine.KcpData);
                await KcpCore.Output(kcpout).ConfigureAwait(false);
                var (buffer, lenght) = kcpout.Pop();
                SocketSend(buffer, lenght);
            }
        }

        public override void Send(int rpcID, object message, object options = null)
        {
            if (Client == null || Closer?.IsDisconnecting == true)
            {
                //当遇到底层不能发送消息的情况下，如果时Rpc发送，直接触发Rpc异常。
                if (rpcID > 0)
                {
                    //对于已经注册了Rpc的消息,直接触发异常。
                    RpcLayer.RpcCallbackPool.TrySetException(rpcID, new SocketException(-1));
                    return;
                }
                else
                {
                    throw new SocketException(-1);
                }
            }

            if (options is IForceUdpDataOnKcpRemote force && force.ForceUdp)
            {
                base.Send(rpcID, message, options);
            }
            else
            {
                if (TrySerialize(SendWriter, rpcID, message, options))
                {
                    var (buffer, lenght) = SendWriter.Pop();
                    KcpCore.Send(buffer.Memory.Span.Slice(0, lenght));
                    buffer.Dispose();
                }
                else
                {
                    var (buffer, lenght) = SendWriter.Pop();
                    buffer.Dispose();
                }
            }
        }
    }

    public partial class KcpRemote
    {
        ///接收===================================================================

        protected UdpBufferWriter kcprecv = new UdpBufferWriter(BufferSizer);
        async void KCPRecv()
        {
            while (true)
            {
                await KcpCore.Recv(kcprecv).ConfigureAwait(false);
                var (buffer, lenght) = kcprecv.Pop();
                if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
                {
                    try
                    {
                        ProcessBody(new ReadOnlySequence<byte>(segment.Array, 0, lenght));
                    }
                    catch (Exception e)
                    {
                        TraceListener?.WriteLine(e);
                    }
                }
                buffer.Dispose();
            }
        }

        //readonly object lockobj = new object();
        internal protected override void RecvKcpData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {
            //lock (lockobj)
            {
                //由于FindRemote 是异步，可能挂起多个RecvKcpData，当异步恢复时，可能导致多线程同时调用此处。
                KcpCore.Input(new ReadOnlySpan<byte>(buffer, start, count));
            }
        }

        protected internal override void RecvKcpData(IPEndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            //lock (lockobj)
            {
                //由于FindRemote 是异步，可能挂起多个RecvKcpData，当异步恢复时，可能导致多线程同时调用此处。
                KcpCore.Input(buffer);
            }
        }
    }
}



