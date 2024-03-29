﻿#if NET7_0_OR_GREATER

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
            var stream = await Quic.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);

            if (RemoteCore.TrySerialize(stream, rpcID, message, options))
            {
                
            }

            stream.Close();
        }

        public Socket Client { get; }
        public bool IsVaild { get; }

        public async Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            QuicClientConnectionOptions option = new();
            option.RemoteEndPoint = endPoint;
            QuicConnection quic = await QuicConnection.ConnectAsync(option,cancellationToken);
            this.Quic = quic;
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
            await stream.WritesClosed.ConfigureAwait(false);
            RemoteCore.ProcessBody(stream);
            stream.Close();
        }
    }
}

#endif