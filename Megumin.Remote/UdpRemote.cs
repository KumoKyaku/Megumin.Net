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
    public partial class UdpRemote:RpcRemote,IRemoteEndPoint, IRemote
    {
        public Socket socket;

        public Guid GUID { get; internal set; }
        public int Password { get; set; } = -1;
        public UdpRemote()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }



        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint { get; }

        
        public Socket Client { get; }
        public bool IsVaild { get; }
        public float LastReceiveTimeFloat { get; }
        public int ID { get; }
    
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
            socket.SendTo(buffer, 0, UdpAuthResponse.Length, SocketFlags.None, endPoint);
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

            internal void WriteHeader(byte header)
            {
                var span = GetSpan(1);
                span[0] = header;
                Advance(1);
            }
        }

        protected virtual Writer SendWriter { get; } = new Writer(8192 * 4);
        protected override async void Send(int rpcID, object message, object options = null)
        {
            SendWriter.WriteHeader(UdpRemoteMessageDefine.Common);
            if (TrySerialize(SendWriter, rpcID, message, options))
            {
                var (buffer, lenght) = SendWriter.Pop();
                if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
                {
                    //todo 异步发送
                    socket.SendTo(segment.Array, 0, lenght, SocketFlags.None, ConnectIPEndPoint);
                }
                buffer.Dispose();
            }
            else
            {
                var (buffer, lenght) = SendWriter.Pop();
                buffer.Dispose();
            }
        }

        //接收============================================================
        
        /// <summary>
        /// 主动侧需要手动开启接收，被动侧由listener接收然后分发
        /// </summary>
        /// <param name="port"></param>
        public async void ClientSideRecv(int port)
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            while (true)
            {
                byte[] cache = new byte[8192];
                ArraySegment<byte> buffer = new ArraySegment<byte>(cache);
                SocketFlags socketFlags = SocketFlags.None;
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var res = await socket.ReceiveFromAsync(buffer, socketFlags, remoteEndPoint);
                InnerDeal(res.RemoteEndPoint as IPEndPoint, buffer.Array);
            }
        }

        void InnerDeal(IPEndPoint endPoint, byte[] recvbuffer)
        {
            byte messageType = recvbuffer[0];
            switch (messageType)
            {
                case UdpRemoteMessageDefine.QuestAuth:
                    DealAuthBuffer(endPoint, recvbuffer);
                    break;
                case UdpRemoteMessageDefine.Answer:
                    //主动侧不处理验证应答。
                    break;
                case UdpRemoteMessageDefine.Test:
                case UdpRemoteMessageDefine.Common:
                    ProcessBody(new ReadOnlySequence<byte>(recvbuffer, 1, recvbuffer.Length - 1));
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 主动侧需要手动开启接收，被动侧由listener接收然后分发
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="span"></param>
        internal protected void ServerSideRecv(IPEndPoint endPoint, byte[] buffer, int offset, int count)
        {
            ConnectIPEndPoint = endPoint;
            ProcessBody(new ReadOnlySequence<byte>(buffer, offset, count));
        }

    }
}
