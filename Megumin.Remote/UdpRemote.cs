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
    /// <summary>
    /// Q：是否报头带上长度验证完整性？
    /// A：不需要，如果数据报重组失败Udp会直接丢弃。
    /// </summary>
    public class UdpRemote:RpcRemote,IRemoteEndPoint
    {
        protected IPEndPoint lastRecvIP;

        Socket socket;

        public Guid GUID { get; internal set; }
        public int Password { get; set; } = -1;
        public UdpRemote()
        {
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            FillRecv();
        }

        private async void FillRecv()
        {
            while (true)
            {
                ArraySegment<byte> buffer = default;
                SocketFlags socketFlags = SocketFlags.None;
                IPEndPoint remoteEndPoint = default;
                var res = await socket.ReceiveFromAsync(buffer, socketFlags, remoteEndPoint);
                InnerDeal(res.RemoteEndPoint as IPEndPoint, buffer.Array);
            }
        }

        protected TestWriter testWriter = new TestWriter(65535);
        protected override async void Send(int rpcID, object message, object options = null)
        {
            testWriter.WriteHeader(UdpRemoteMessageDefine.Common);
            if (TrySerialize(testWriter, rpcID,message,options))
            {
                var (buffer,lenght) = testWriter.Pop();
                if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
                {
                    socket.SendTo(segment.Array, 0, lenght, SocketFlags.None, ConnectIPEndPoint);
                }
                buffer.Dispose();
            }
            else
            {
                var (buffer, lenght) = testWriter.Pop();
                buffer.Dispose();
            }
        }

        /// <summary>
        /// todo 自动扩容
        /// </summary>
        protected class TestWriter: IBufferWriter<byte>
        {
            private int defaultCount;
            private IMemoryOwner<byte> buffer;
            int offset = 0;

            public TestWriter(int bufferLenght)
            {
                this.defaultCount = bufferLenght;
                buffer = MemoryPool<byte>.Shared.Rent(defaultCount);
            }

            /// <summary>
            /// 弹出一个序列化完毕的缓冲。
            /// </summary>
            /// <returns></returns>
            public (IMemoryOwner<byte>,int) Pop()
            {
                var old = buffer;
                var lenght = offset;
                buffer = MemoryPool<byte>.Shared.Rent(defaultCount);
                offset = 0;
                return (old,lenght);
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

        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint { get; }

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
        /// 处理认证
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="recvbuffer"></param>
        private void DealAuthBuffer(IPEndPoint endPoint, byte[] recvbuffer)
        {
            var auth = QuestAuth.Deseire(recvbuffer);
            //创建认证回复消息
            Answer answer = new Answer();

            if (Password == -1)
            {
                this.GUID = auth.Guid;
                this.Password = auth.PassWord;
                answer.IsNew = true;
            }

            answer.Guid = this.GUID;
            answer.PassWord = Password;
            byte[] buffer = new byte[Answer.Length];
            answer.Sieralize(buffer);
            socket.SendTo(buffer, 0, Answer.Length, SocketFlags.None, endPoint);
        }

        /// <summary>
        /// 被动侧统一接收分发下来的消息。
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="span"></param>
        internal void ServerSideRecv(IPEndPoint endPoint, byte[] buffer,int offset,int count)
        {
            ConnectIPEndPoint = endPoint;
            ProcessBody(new ReadOnlySequence<byte>(buffer,offset,count));
        }
    }
}
