using Net.Remote;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public partial class UdpRemote : RpcRemote, IRemoteEndPoint, IRemote,IConnectable
    {
        public int ID { get; } = InterlockedID<IRemoteID>.NewID();
        public Guid GUID { get; internal set; }
        public int Password { get; set; } = -1;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public virtual EndPoint RemappedEndPoint => ConnectIPEndPoint;
        public Socket Client { get; protected set; }
        public bool IsVaild { get; internal protected set; }
        public float LastReceiveTimeFloat { get; }
        public UdpRemoteListener Listener { get; internal set; }
        public UdpRemote()
        {
            Client = new Socket(SocketType.Dgram, ProtocolType.Udp);
        }

        //连接认证部分================================================

        /// <summary>
        /// 处理认证
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="recvbuffer"></param>
        protected virtual void DealAuthBuffer(IPEndPoint endPoint, byte[] recvbuffer)
        {
            var auth = UdpAuthRequest.Deserialize(recvbuffer);
            //创建认证回复消息
            UdpAuthResponse answer = new UdpAuthResponse();

            if (Password == -1)
            {
                this.GUID = auth.Guid;
                this.Password = auth.Password;
                answer.IsNew = true;
            }

            answer.Guid = this.GUID;
            answer.Password = Password;
            byte[] buffer = new byte[UdpAuthResponse.Length];
            answer.Serialize(buffer);
            Client.SendTo(buffer, 0, UdpAuthResponse.Length, SocketFlags.None, endPoint);
        }


        //发送==========================================================
        protected class Writer : IBufferWriter<byte>
        {
            private int defaultCount;
            private IMemoryOwner<byte> buffer;
            int offset = 0;

            public Writer(int bufferLenght)
            {
                this.defaultCount = bufferLenght;
                buffer = MemoryPool<byte>.Shared.Rent(defaultCount);
            }

            /// <summary>
            /// 弹出一个序列化完毕的缓冲。
            /// </summary>
            /// <returns></returns>
            public (IMemoryOwner<byte>, int) Pop()
            {
                var old = buffer;
                var lenght = offset;
                buffer = MemoryPool<byte>.Shared.Rent(defaultCount);
                offset = 0;
                return (old, lenght);
            }

            public void Advance(int count)
            {
                offset += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                return buffer.Memory.Slice(offset, sizeHint);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                return buffer.Memory.Span.Slice(offset, sizeHint);
            }

            public void WriteHeader(byte header)
            {
                var span = GetSpan(1);
                span[0] = header;
                Advance(1);
            }
        }

        protected virtual Writer SendWriter { get; } = new Writer(8192 * 4);

        protected override void Send(int rpcID, object message, object options = null)
        {
            SendWriter.WriteHeader(UdpRemoteMessageDefine.Common);
            if (TrySerialize(SendWriter, rpcID, message, options))
            {
                var (buffer, lenght) = SendWriter.Pop();
                SocketSend(buffer, lenght);
            }
            else
            {
                var (buffer, lenght) = SendWriter.Pop();
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 网络层实际发送数据位置
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="lenght"></param>
        protected async void SocketSend(IMemoryOwner<byte> buffer, int lenght)
        {
            if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
            {
                var target = new ArraySegment<byte>(segment.Array, 0, lenght);
                await Client.SendToAsync(target, SocketFlags.None, ConnectIPEndPoint)
                    .ConfigureAwait(false);
            }

            buffer.Dispose();
        }

        //接收============================================================

        /// <summary>
        /// 主动侧需要手动开启接收，被动侧由listener接收然后分发
        /// </summary>
        /// <param name="port"></param>
        public async void ClientSideRecv(int port)
        {
            Client.Bind(new IPEndPoint(IPAddress.Any, port));
            IsVaild = true;
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                var cache = ArrayPool<byte>.Shared.Rent(8192);
                ArraySegment<byte> buffer = new ArraySegment<byte>(cache);
                var res = await Client.ReceiveFromAsync(
                    buffer, SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
                InnerDeal(res.RemoteEndPoint as IPEndPoint, cache, 0, res.ReceivedBytes);
                ArrayPool<byte>.Shared.Return(cache);
            }
        }

        protected virtual void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer, int start, int count)
        {
            byte messageType = recvbuffer[start];
            switch (messageType)
            {
                case UdpRemoteMessageDefine.UdpAuthRequest:
                    DealAuthBuffer(endPoint, recvbuffer);
                    break;
                case UdpRemoteMessageDefine.UdpAuthResponse:
                    //主动侧不处理验证应答。
                    break;
                case UdpRemoteMessageDefine.LLMsg:
                case UdpRemoteMessageDefine.Common:
                    RecvPureBuffer(recvbuffer, start + 1, count - 1);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 主动侧需要手动开启接收，被动侧由listener接收然后分发
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        internal protected virtual void ServerSideRecv(IPEndPoint endPoint, byte[] buffer, int offset, int count)
        {
            ConnectIPEndPoint = endPoint;
            RecvPureBuffer(buffer, offset + 1, count - 1);
        }

        protected virtual void RecvPureBuffer(byte[] buffer, int start, int count)
        {
            ProcessBody(new ReadOnlySequence<byte>(buffer, start, count));
        }

        protected virtual void RecvPureBuffer(ReadOnlySequence<byte> sequence)
        {
            ProcessBody(sequence);
        }

        //*********************************心跳处理
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            if (messageID == MSGID.HeartbeatsMessageID)
            {
                return new ValueTask<object>(message);
            }
            return base.OnReceive(cmd, messageID, message);
        }

        int MissHearCount = 0;
        async void SendBeat()
        {
            MessageLUT.Regist(Heartbeat.Default);
            while (true)
            {
                MissHearCount += 1;
                var (_, exception) = await Send<Heartbeat>(Heartbeat.Default);
                if (exception == null)
                {
                    MissHearCount = 0;
                }

                if (MissHearCount >= 5)
                {
                    MissHearCount = 0;
                    break;
                    //触发断开。TODO
                }
                await Task.Delay(2000);
            }
        }

        public Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            ConnectIPEndPoint = endPoint;
            return Task.CompletedTask;
        }

        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {
            
        }
    }
}
