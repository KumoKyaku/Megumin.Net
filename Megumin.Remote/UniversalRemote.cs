﻿using Net.Remote;
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

        public void SetTransport(BaseTransport transport)
        {
            transport.RemoteCore = this;
            if (transport is ITransportable transportable)
            {
                Transport = transportable;
            }
            else
            {
                throw new Exception();
            }
        }

        public int ID { get; } = InterlockedID<IRemoteID>.NewID();
    }

}





