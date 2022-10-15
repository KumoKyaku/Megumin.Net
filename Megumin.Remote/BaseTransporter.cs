using System;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
{
    public class BaseTransporter
    {
        public RpcRemote RemoteCore { get; set; }
        public System.Diagnostics.TraceListener TraceListener { get; set; }
    }
}


