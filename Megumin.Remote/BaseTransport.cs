using System;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
{
    public class BaseTransport
    {
        public RpcRemote RemoteCore { get; set; }
        public System.Diagnostics.TraceListener TraceListener { get; set; }
    }
}


