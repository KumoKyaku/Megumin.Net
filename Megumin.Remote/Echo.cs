using Net.Remote;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote.Simple
{
    /// <summary>
    /// Tcp回声远端
    /// </summary>
    public class EchoTcp:TcpRemote
    {
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return new ValueTask<object>(message);
        }
    }

    public class EchoUdp : UdpRemote
    {
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return new ValueTask<object>(message);
        }
    }

    public class EchoKcp : KcpRemote
    {
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return new ValueTask<object>(message);
        }
    }

    /////如果不是用回调函数，那么就不能把echoRemote抽象成一个。
    //public class Echo<R> : R
    //    where R:RemoteBase
    //{
    //    public IRemote 
    //}

    internal interface IRecvCallback
    {
        ValueTask<object> OnReceive(short cmd, int messageID, object message);
    }

    internal class EchoCallback: IRecvCallback
    {
        public ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return new ValueTask<object>(message);
        }
    }

    internal class TcpRemote<T> : TcpRemote
        where T : IRecvCallback, new ()
    {
        T call = new T();
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return call.OnReceive(cmd, messageID, message);
        }
    }
}


