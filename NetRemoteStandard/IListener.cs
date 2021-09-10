using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Net.Remote
{
    public interface IListener<in T>
        where T : IRemote
    {
        ValueTask<R> ListenAsync<R>(Func<R> createFunc) where R : T;
    }
}


