using Net.Remote;

namespace Megumin.Remote
{
    public class BaseTransport
    {
        public RpcRemote RemoteCore { get; set; }
        public System.Diagnostics.TraceListener TraceListener { get; set; }

        public virtual bool ReConnectFrom(ITransportable transportable)
        {
            return true;
        }
    }
}


