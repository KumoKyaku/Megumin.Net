using System;
using System.Net;
using System.Threading.Tasks;

namespace Net.Remote
{
    public interface IListenerOld<in T>
        where T : IRemote
    {
        ValueTask<R> ListenAsync<R>(Func<R> createFunc) where R : T;
    }

    public interface IListener<in T>
        where T : IRemote
    {
        IPEndPoint ConnectIPEndPoint { get; set; }
        ValueTask<Remote> ReadAsync<Remote>(Func<Remote> createFunc) where Remote : T;
        void Start(object option = null);
        void Stop();

#if NETSTANDARD2_0_OR_GREATER || NET5_0_OR_GREATER
        System.Diagnostics.TraceListener TraceListener { get; set; }
#endif
    }
}


