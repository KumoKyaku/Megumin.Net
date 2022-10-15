using System;
using System.Net;
using System.Threading.Tasks;

namespace Net.Remote
{
    //public interface IListenerOld<in T>
    //    where T : IRemote
    //{
    //    ValueTask<R> ListenAsync<R>(Func<R> createFunc) where R : T;
    //}

    public interface IListener
    {
        IPEndPoint ConnectIPEndPoint { get; set; }
        void Start(object option = null);
        void Stop();
        ValueTask ReadAsync(IRemote remote);

#if NETSTANDARD2_0_OR_GREATER || NET5_0_OR_GREATER
        System.Diagnostics.TraceListener TraceListener { get; set; }
#endif
    }

    //public interface IListener<in T> : IListener
    //    where T : IRemote
    //{
    //    ValueTask<Remote> ReadAsync<Remote>(Func<Remote> createFunc) where Remote : T;

    //}
}


