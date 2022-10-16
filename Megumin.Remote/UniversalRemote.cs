using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public class UniversalRemote : RpcRemote
    {
        
    }

    /// <summary>
    /// Tcp回声远端
    /// </summary>
    public class EchoRemote : RpcRemote
    {
        public override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return new ValueTask<object>(message);
        }
    }

    public class LogExampleRemote : RpcRemote
    {
        public override ValueTask<Result> SendAsyncSafeAwait<T, Result>(T message, object options = null, Action<Exception> onException = null)
        {
            if (onException == null)
            {
                //保存原始调用处堆栈。
                string stack = new StackTrace().ToString();
                onException += (ex) =>
                {
                    string err = ex + stack;
                    Debug.Write(err);
                };
            }
            return base.SendAsyncSafeAwait<T, Result>(message, options, onException);
        }
    }
}





