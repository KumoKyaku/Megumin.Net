﻿using Megumin.Message;
using Net.Remote;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public partial class UdpRemote : RpcRemote, IRemoteEndPoint, IRemote, IConnectable
    {
        public int ID { get; } = InterlockedID<IRemoteID>.NewID();
        public Guid? GUID { get; internal set; } = null;
        public int? Password { get; set; } = null;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public virtual EndPoint RemappedEndPoint => ConnectIPEndPoint;
        public Socket Client { get; internal protected set; }
        public bool IsVaild { get; internal protected set; }
        public float LastReceiveTimeFloat { get; }
        public UdpRemoteListener Listener { get; internal set; }
        /// <summary>
        /// 为kcp预留
        /// </summary>
        protected int KcpIOChannel { get; set; }
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

            if (!this.GUID.HasValue)
            {
                this.GUID = auth.Guid;
            }

            answer.Guid = this.GUID.Value;
            answer.Password = Password.Value;
            answer.KcpChannel = KcpIOChannel;
            byte[] buffer = new byte[UdpAuthResponse.Length];
            answer.Serialize(buffer);
            Client.SendTo(buffer, 0, UdpAuthResponse.Length, SocketFlags.None, endPoint);
        }

        //发送==========================================================

        protected virtual UdpSendWriter SendWriter { get; } = new UdpSendWriter(8192 * 4);

        public override void Send(int rpcID, object message, object options = null)
        {
            SendWriter.WriteHeader(UdpRemoteMessageDefine.UdpData);
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
        public async void ClientSideRecv()
        {
            //Client.Bind(new IPEndPoint(IPAddress.Any, port));
            IsVaild = true;
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                var cache = ArrayPool<byte>.Shared.Rent(8192);
                ArraySegment<byte> buffer = new ArraySegment<byte>(cache);
                try
                {
                    var res = await Client.ReceiveFromAsync(
                    buffer, SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
                    InnerDeal(res.RemoteEndPoint as IPEndPoint, cache, 0, res.ReceivedBytes);
                }
                catch (Exception e)
                {
                    Logger?.Log(e.ToString());
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(cache);
                }
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
                case UdpRemoteMessageDefine.LLData:
                    RecvLLData(endPoint, recvbuffer, start + 1, count - 1);
                    break;
                case UdpRemoteMessageDefine.UdpData:
                    RecvUdpData(endPoint, recvbuffer, start + 1, count - 1);
                    break;
                case UdpRemoteMessageDefine.KcpData:
                    RecvKcpData(endPoint, recvbuffer, start + 1, count - 1);
                    break;
                default:
                    break;
            }
        }

        internal protected virtual void RecvLLData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {
            ProcessBody(new ReadOnlySequence<byte>(buffer, start, count));
        }

        internal protected virtual void RecvUdpData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {
            ProcessBody(new ReadOnlySequence<byte>(buffer, start, count));
        }

        internal protected virtual void RecvKcpData(IPEndPoint endPoint, byte[] buffer, int start, int count)
        {

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

        static byte[] conn = new byte[1];
        public virtual Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            if (!Password.HasValue)
            {
                Password = new Random().Next(1000, 10000);
            }
            ConnectIPEndPoint = endPoint;
            Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            //Client.SendTo(conn, endPoint);//承担bind作用，不然不能recv。
            ClientSideRecv();
            return Task.CompletedTask;
        }

        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {

        }

        internal protected virtual void Recv0(IPEndPoint endPoint)
        {
            
        }
    }
}
