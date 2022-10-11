using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Net.Remote
{
    public interface IListener<in T>
        where T : IRemote
    {
        ValueTask<R> ListenAsync<R>(Func<R> createFunc) where R : T;
    }

    public interface IListener2<in T>
        where T : IRemote
    {
        IPEndPoint ConnectIPEndPoint { get; set; }
        ValueTask<R> ReadAsync<R>(Func<R> createFunc) where R : T;
        void Start(object option = null);
        void Stop();

#if NETSTANDARD2_0_OR_GREATER || NET5_0_OR_GREATER
        System.Diagnostics.TraceListener TraceListener { get; set; }
#endif
    }
}


