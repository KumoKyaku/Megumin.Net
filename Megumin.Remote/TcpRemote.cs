using Megumin.Message;
using Net.Remote;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace Megumin.Remote
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>消息报头结构：
    /// Lenght(总长度，包含自身报头) [int] [4] + RpcID [int] [4] + CMD [short] [2] + MessageID [int] [4]</remarks>
    public partial class TcpRemote:IRemote
    {
        public int ID { get; } = InterlockedID<IRemote>.NewID();
        /// <summary>
        /// 这是留给用户赋值的
        /// </summary>
        public virtual int UID { get; set; }
        public bool IsVaild { get; protected set; } = true;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public DateTime LastReceiveTime { get; protected set; } = DateTime.Now;
        public RpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);

        public Socket Client { get; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;


        /// <summary>
        /// Mono/IL2CPP 请使用中使用<see cref="TcpRemoteold.TcpRemoteold(AddressFamily)"/>
        /// </summary>
        public TcpRemote() 
            : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {

        }

        /// <remarks>
        /// <para>SocketException: Protocol option not supported</para>
        /// http://www.schrankmonster.de/2006/04/26/system-net-sockets-socketexception-protocol-not-supported/
        /// </remarks>
        public TcpRemote(AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {

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

        /// <summary>
        /// 认证阶段
        /// </summary>
        protected virtual ValueTask<int> Auth()
        {
            return new ValueTask<int>(0);
        }

        /// <summary>
        /// 开始工作
        /// </summary>
        protected virtual void WorkStart()
        {
            ReceiveStart();
            SendStart();
        }
    }

    public partial class TcpRemote : IConnectable
    {
        /// <summary>
        /// 连接保护器，防止多次调用
        /// </summary>
        readonly object _connectlock = new object();
        /// <summary>
        /// 正在连接
        /// </summary>
        bool IsConnecting = false;
        private async Task ConnectAsync(Socket socket, IPEndPoint endPoint, int retryCount = 0)
        {
            lock (_connectlock)
            {
                if (IsConnecting)
                {
                    throw new InvalidOperationException("连接正在进行中");
                }
                IsConnecting = true;
            }

            if (socket.Connected)
            {
                throw new ArgumentException("socket已经连接");
            }

            while (retryCount >= 0)
            {
                try
                {
                    await Client.ConnectAsync(endPoint);
                    IsConnecting = false;
                }
                catch (Exception)
                {
                    if (retryCount <= 0)
                    {
                        IsConnecting = false;
                        throw;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }
        }

        public Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            ConnectIPEndPoint = endPoint;
            return ConnectAsync(Client, endPoint, retryCount);
        }

        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 断开连接之后
        /// </summary>
        protected virtual void PostDisconnect()
        {

        }
    }

    public partial class TcpRemote : ISendable, ISendCanAwaitable
    {
        /// <summary>
        /// 开始发送消息
        /// </summary>
        public async void SendStart()
        {
            var target = await SendPipe.PeekNext();
            var length = target.SendMemory.Length;
            var result = await Client.SendAsync(target.SendMemory, SocketFlags.None);
            if (result == length)
            {
                //dequeue?
                //成功？
                target.SendSuccess();
            }

            //todo 发送失败。
            SendStart();
        }

        /// <summary>
        /// 发送管道
        /// </summary>
        /// <remarks>发送管道没有涵盖所有案例，尽量不要给外界访问</remarks>
        protected TcpSendPipe SendPipe { get; } = new TcpSendPipe();

        protected virtual void Send(int rpcID, object message, object options = null)
        {
            var writer = SendPipe.GetNewwriter();
            if (TrySerialize(writer, rpcID, message, options))
            {
                //序列化成功
                writer.PackSuccess();
            }
            else
            {
                //序列化失败
                writer.Discard();
            }

            SendStart();
        }

        /// <summary>
        /// 序列化消息
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual bool TrySerialize(IBufferWriter<byte> writer, int rpcID, object message, object options = null)
        {
            try
            {
                //写入rpcID CMD
                var span = writer.GetSpan(10);
                span.Write(rpcID);
                //有CMD 长度2预留 
                writer.Advance(10);

                int messageID = MessageLUT.Serialize(writer, message, options);
                //补写消息ID到指定位置。 前面已经Advance了，这里不在Advance。
                span.Slice(6).Write(messageID);

                return true;
            }
            catch (Exception)
            {
                //todo log;
                return false;
            }
        }

        /// <summary>
        /// 序列化消息
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcID"></param>
        /// <param name="sequence"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual bool TrySerialize(IBufferWriter<byte> writer, int rpcID, in ReadOnlySequence<byte> sequence, object options = null)
        {
            try
            {
                //写入rpcID CMD
                var span = writer.GetSpan(6);
                span.Write(rpcID);
                //有CMD 长度2预留 
                writer.Advance(6);

                foreach (var item in sequence)
                {
                    writer.Write(item.Span);
                }

                return true;
            }
            catch (Exception)
            {
                //todo log;
                return false;
            }
        }

        public void Send(object message, object options = null)
        {
            Send(0, message, options);
        }

        public IMiniAwaitable<(RpcResult result, Exception exception)>
            Send<RpcResult>(object message, object options = null)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(options);

            try
            {
                Send(rpcID, message);
                return source;
            }
            catch (Exception e)
            {
                RpcCallbackPool.TrySetException(rpcID * -1, e);
                return source;
            }
        }

        public IMiniAwaitable<RpcResult> SendSafeAwait<RpcResult>
            (object message, Action<Exception> OnException = null, object options = null)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(OnException, options);

            try
            {
                Send(rpcID, message);
                return source;
            }
            catch (Exception e)
            {
                source.CancelWithNotExceptionAndContinuation();
                OnException?.Invoke(e);
                return source;
            }
        }
    }

    public partial class TcpRemote : IReceiveMessage, IObjectMessageReceiver
    {
        /// <summary>
        /// 开始接收消息；
        /// </summary>
        private void ReceiveStart()
        {
            throw new NotImplementedException();
        }

        public float LastReceiveTimeFloat { get; } = float.MaxValue;     

        public virtual ValueTask<object> Deal(int rpcID, object message)
        {
            throw new NotImplementedException();
        }
    }
}
