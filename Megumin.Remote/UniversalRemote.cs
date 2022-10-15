using Net.Remote;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Megumin.Message;
using System.ComponentModel;

namespace Megumin.Remote
{
    public class UniversalRemote : RpcRemote, IRemote
    {
        public override void Send(int rpcID, object message, object options = null)
        {
            Transport?.Send(rpcID, message, options);
        }

        public ITransportable Transport { get; private set; }

        public void SetTransport(BaseTransporter transporter)
        {
            transporter.RemoteCore = this;
            if (transporter is ITransportable transportable)
            {
                Transport = transportable;
            }
            else
            {
                throw new Exception();
            }
        }

        public Socket Client { get; }
        public bool IsVaild { get; set; }
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint { get; }
        public EndPoint RemoteEndPoint { get; }
        public DateTimeOffset LastReceiveTime { get; }
        public int ID { get; }
    }

}





