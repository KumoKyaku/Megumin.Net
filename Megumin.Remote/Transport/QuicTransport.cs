#if NET7_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Quic;
using Net.Remote;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Runtime.Versioning;
using System.Buffers;

namespace Megumin.Remote
{
    [RequiresPreviewFeatures]
    public class QuicTransport : BaseTransport, ITransportable
    {


        public async void Send<T>(T message, int rpcID, object options = null)
        {
            await Steam.WriteAsync(new byte[0x10000]);
            Steam.Close();
        }

        public Socket Client { get; }
        public bool IsVaild { get; }

        public async Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            QuicClientConnectionOptions option = new();
            option.RemoteEndPoint = endPoint;
            QuicConnection quic = await QuicConnection.ConnectAsync(option,cancellationToken);
            this.Quic = quic;
            this.Steam = await quic.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
        }

        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint { get; }
        public EndPoint RemoteEndPoint { get; }

        public void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false)
        {
            Quic.DisposeAsync();
        }

        public bool IsSocketSending { get; }
        public QuicConnection Quic { get; private set; }
        public QuicStream Steam { get; private set; }

        public void StartSocketSend()
        {
            throw new NotImplementedException();
        }

        public void StopSocketSend()
        {
            throw new NotImplementedException();
        }

        public async void Recv()
        {
            //每个消息一个流可不可以？
            var stream = await Quic.AcceptInboundStreamAsync();
            //等发送完一起读取
            await stream.WritesClosed;

            while (stream.CanRead)
            {
                //循环读取知道buffer是0？
                //还是直接将流放到反序列函数里去。
                var buffer = await stream.ReadAsync(new byte[0x10000]);
            }
        }
    }
}

#endif