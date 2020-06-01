using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Net.Remote;
using Megumin.Message;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ByteMessageList = Megumin.Remote.ListPool<System.Buffers.IMemoryOwner<byte>>;

namespace Megumin.Remote
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal static class ListPool<T>
    {
        static ConcurrentQueue<List<T>> pool = new ConcurrentQueue<List<T>>();

        /// <summary>
        /// 默认容量10
        /// </summary>
        public static int MaxSize { get; set; } = 10;

        public static List<T> Rent()
        {
            if (pool.TryDequeue(out var list))
            {
                if (list == null || list.Count != 0)
                {
                    return new List<T>();
                }
                return list;
            }
            else
            {
                return new List<T>();
            }
        }

        /// <summary>
        /// 调用者保证归还后不在使用当前list
        /// </summary>
        /// <param name="list"></param>
        public static void Return(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            if (pool.Count < MaxSize)
            {
                list.Clear();
                pool.Enqueue(list);
            }
        }

        public static void Clear()
        {
            while (pool.Count > 0)
            {
                pool.TryDequeue(out var list);
            }
        }

    }

    /// <summary>
    /// <para>TcpChannel内存开销 整体采用内存池优化</para>
    /// <para>发送内存开销 对于TcpChannel实例 动态内存开销，取决于发送速度，内存实时占用为发送数据的1~2倍</para>
    /// <para>                  接收的常驻开销8kb*2,随着接收压力动态调整</para>
    /// </summary>
    public partial class TcpRemote : RemoteBase,  IRemote
    {
        public Socket Client { get; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;

        /// <summary>
        /// Mono/IL2CPP 请使用中使用<see cref="TcpRemote.TcpRemote(AddressFamily)"/>
        /// </summary>
        public TcpRemote() : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {

        }

        /// <remarks>
        /// <para>SocketException: Protocol option not supported</para>
        /// http://www.schrankmonster.de/2006/04/26/system-net-sockets-socketexception-protocol-not-supported/
        /// </remarks>
        public TcpRemote(AddressFamily addressFamily) 
            : this(new Socket(addressFamily,SocketType.Stream, ProtocolType.Tcp))
        {

        }

        public TcpRemote(IMessagePipeline messagePipeline) : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
            MessagePipeline = messagePipeline;
        }

        public TcpRemote(IMessagePipeline messagePipeline, AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
            MessagePipeline = messagePipeline;
        }

        /// <summary>
        /// 使用一个已连接的Socket创建远端
        /// </summary>
        /// <param name="client"></param>
        internal TcpRemote(Socket client)
        {
            this.Client = client;
            IsVaild = true;
        }

        void OnSocketException(SocketError error)
        {
            TryDisConnectSocket();
            OnDisConnect?.Invoke(error);
        }

        void TryDisConnectSocket()
        {
            try
            {
                if (Client.Connected)
                {
                    Client.Shutdown(SocketShutdown.Both);
                    Client.Disconnect(false);
                    Client.Close();
                }
            }
            catch (Exception)
            {
                //todo
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    try
                    {
                        if (Client.Connected)
                        {
                            Disconnect();
                        }
                    }
                    catch (Exception)
                    {

                    }
                    finally
                    {
                        Client?.Dispose();
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。
                IsVaild = false;
                lock (sendlock)
                {
                    while (sendWaitList.TryDequeue(out var owner))
                    {
                        owner?.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~TcpRemote()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    ///连接 断开连接
    partial class TcpRemote:IConnectable
    {
        public event Action<SocketError> OnDisConnect;

        bool isConnecting = false;
        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            if (isConnecting)
            {
                return new Exception("Connection in progress/连接正在进行中");
            }
            isConnecting = true;
            this.ConnectIPEndPoint = endPoint;
            while (retryCount >= 0)
            {
                try
                {
                    await Client.ConnectAsync(ConnectIPEndPoint);
                    isConnecting = false;
                    ReceiveStart();
                    return null;
                }
                catch (Exception e)
                {
                    if (retryCount <= 0)
                    {
                        isConnecting = false;
                        return e;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }

            isConnecting = false;
            return new NullReferenceException();
        }

        public void Disconnect()
        {
            IsVaild = false;
            manualDisconnecting = true;
            TryDisConnectSocket();
        }
    }

    /// 发送实例消息
    partial class TcpRemote
    {
        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer) => Client.SendAsync(msgBuffer, SocketFlags.None);
    }

    /// 发送字节消息
    partial class TcpRemote
    {
        ConcurrentQueue<IMemoryOwner<byte>> sendWaitList = new ConcurrentQueue<IMemoryOwner<byte>>();
        bool isSending;
        private MemoryArgs sendArgs;
        protected readonly object sendlock = new object();

        SendStates State = SendStates.Idel;

        enum SendStates
        {
            Idel,
            MoveWaiting2Sending,
            Sending,
            Release,
        }


        /// <summary>
        /// 注意，发送完成时内部回收了buffer。
        /// ((框架约定1)发送字节数组发送完成后由发送逻辑回收)
        /// </summary>
        /// <param name="bufferMsg"></param>
        public override void SendAsync(IMemoryOwner<byte> bufferMsg)
        {
            sendWaitList.Enqueue(bufferMsg);
            SendStart();
        }

        /// <summary>
        /// 检测是否应该发送
        /// </summary>
        /// <returns></returns>
        bool CheckCanSend()
        {
            if (!Client.Connected)
            {
                return false;
            }

            ///如果待发送队列有消息，交换列表 ，继续发送
            lock (sendlock)
            {
                if (!sendWaitList.IsEmpty && !manualDisconnecting && isSending == false)
                {
                    isSending = true;
                    return true;
                }
            }

            return false;
        }

        void SendStart()
        {
            if (!CheckCanSend())
            {
                return;
            }

            if (sendArgs == null)
            {
                sendArgs = new MemoryArgs();
                sendArgs.Completed += SendComplete;
            }

            //移动等待队列到发送队列；
            while (sendWaitList.TryDequeue(out var owner) || !sendWaitList.IsEmpty)
            {
                sendArgs.SetMemoryOwner((owner,owner.Memory.Length));
            }

            sendArgs.Flush();

            if (!Client.SendAsync(sendArgs))
            {
                SendComplete(Client, sendArgs);
            }
        }

        void SendComplete(object sender, SocketAsyncEventArgs args)
        {
            ///无论成功失败，都要清理发送缓冲
            sendArgs.Release();
            ///这个方法由IOCP线程调用。需要尽快结束。
            isSending = false;

            if (args.SocketError == SocketError.Success)
            {
                ///冗余调用，可以省去
                //args.BufferList = null;

                SendStart();
            }
            else
            {
                SocketError socketError = args.SocketError;
                args = null;
                if (!manualDisconnecting)
                {
                    ///遇到错误
                    OnSocketException(socketError);
                }
            }
        }
    }

    /// 接收字节消息
    partial class TcpRemote : IReceiveMessage
    {
        bool isReceiving;
        SocketAsyncEventArgs receiveArgs;
        /// <summary>
        /// 线程安全的，多次调用不应该发生错误
        /// </summary>
        /// <remarks> 使用TaskAPI 的本地Loopback 接收峰值能达到60,000,000 字节每秒。
        /// 不使用TaskAPI 的本地Loopback 接收峰值能达到200,000,000 字节每秒。可以稳定在每秒6000 0000字节每秒。
        /// 不是严格的测试，但是隐约表明异步task方法不适合性能敏感区域。
        /// </remarks>
        public override void ReceiveStart()
        {
            if (!Client.Connected || isReceiving || disposedValue)
            {
                return;
            }

            isReceiving = true;
            InnerReveiveStart();
        }

        void InnerReveiveStart()
        {
            if (receiveArgs == null)
            {
                receiveArgs = new SocketAsyncEventArgs();
                var bfo = BufferPool.Rent(MaxBufferLength);

                if (MemoryMarshal.TryGetArray<byte>(bfo.Memory, out var buffer))
                {
                    receiveArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                    receiveArgs.Completed += ReceiveComplete;
                    receiveArgs.UserToken = bfo;
                }
                else
                {
                    throw new ArgumentException();
                }
            }

            if (!Client.ReceiveAsync(receiveArgs))
            {
                ReceiveComplete(this, receiveArgs);
            }
        }

        void ReceiveComplete(object sender, SocketAsyncEventArgs args)
        {
            IMemoryOwner<byte> owner = args.UserToken as IMemoryOwner<byte>;

            try
            {
                if (args.SocketError == SocketError.Success)
                {
                    ///本次接收的长度
                    int length = args.BytesTransferred;

                    if (length == 0)
                    {
                        args.Completed -= ReceiveComplete;
                        args = null;
                        OnSocketException(SocketError.Shutdown);
                        isReceiving = false;
                        return;
                    }

                    LastReceiveTime = DateTime.Now;
                    //////有效消息长度
                    int totalValidLength = length + args.Offset;

                    var list = ByteMessageList.Rent();
                    ///由打包器处理分包
                    var residual = MessagePipeline.CutOff(args.Buffer.AsSpan(0,totalValidLength), list);

                    ///租用新内存
                    var bfo = BufferPool.Rent(MaxBufferLength);

                    if (MemoryMarshal.TryGetArray<byte>(bfo.Memory, out var newBuffer))
                    {
                        args.UserToken = bfo;
                    }
                    else
                    {
                        throw new ArgumentException();
                    }

                    if (residual.Length > 0)
                    {
                        ///半包复制
                        residual.CopyTo(bfo.Memory.Span);
                    }

                    args.SetBuffer(newBuffer.Array, residual.Length, newBuffer.Count - residual.Length);


                    ///这里先处理消息在继续接收，处理消息是异步的，耗时并不长，下N次继续接收消息都可能是同步完成，
                    ///先接收可能导致比较大的消息时序错位。

                    ///处理消息
                    DealMessageAsync(list);

                    ///继续接收
                    InnerReveiveStart();
                }
                else
                {
                    args.Completed -= ReceiveComplete;
                    SocketError socketError = args.SocketError;
                    args = null;
                    if (!manualDisconnecting)
                    {

                        OnSocketException(socketError);
                    }
                    isReceiving = false;
                }
            }
            finally
            {
                ///重构后的BufferPool改为申请时清零数据，所以出不清零，节省性能。
                ///owner.Memory.Span.Clear();
                owner.Dispose();
            }
        }

        
        private void DealMessageAsync(List<IMemoryOwner<byte>> list)
        {
            if (list.Count == 0)
            {
                return;
            }
            //todo 排序
            Task.Run(() =>
            {
                foreach (var item in list)
                {
                    ReceiveByteMessage(item);
                }

                ///回收池对象
                list.Clear();
                ByteMessageList.Return(list);
            });
        }
    }

    internal class MemoryArgs : SocketAsyncEventArgs
    {
        public void SetMemoryOwner((IMemoryOwner<byte> memoryOwner,int count) sendbuffer)
        {
            if (sendbuffer.memoryOwner == null)
            {
                return;
            }

            bool TryAdd((IMemoryOwner<byte> memoryOwner,int count) buffer)
            {
                if (MemoryMarshal.TryGetArray<byte>(sendbuffer.memoryOwner.Memory, out var sbuffer))
                {
                    sendList.Add(new ArraySegment<byte>(sbuffer.Array, sbuffer.Offset, sendbuffer.count));
                    releaseList.Add(sendbuffer.memoryOwner);
                    return true;
                }

                return false;
            }


            if (!TryAdd(sendbuffer))
            {
                var temp = BufferPool.Rent(sendbuffer.count);
                sendbuffer.memoryOwner.Memory.Span.Slice(0, sendbuffer.count).CopyTo(temp.Memory.Span);
                if (!TryAdd((temp,sendbuffer.count)))
                {
                    throw new Exception($"内存池损坏");
                }
            }

        }

        public void Flush()
        {
            BufferList = sendList;
        }

        public void Release()
        {
            sendList.Clear();
            foreach (var item in releaseList)
            {
                try
                {
                    item.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            releaseList.Clear();
        }

        readonly List<IMemoryOwner<byte>> releaseList
            = new List<IMemoryOwner<byte>>();

        List<ArraySegment<byte>> sendList = new List<ArraySegment<byte>>();
    }
}
