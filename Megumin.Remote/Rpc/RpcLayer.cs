using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote.Rpc
{
    public interface IRpcCallback
    {
        ValueTask<object> OnReceive(object message);
        void Send(int rpcID, object reply);
    }

    internal class RpcLayer
    {
        public IRpcCallback Callback { get; set; }
        public ObjectRpcCallbackPool RpcCallbackPool { get; } = new ObjectRpcCallbackPool();

        public async void Input(int rpcID, object message)
        {
            if (rpcID < 0)
            {
                //这个消息是rpc返回（回复的RpcID为负数）
                RpcCallbackPool?.TrySetResult(rpcID * -1, message);
            }
            else
            {
                //这个消息非Rpc返回
                //普通响应onRely
                var reply = await Callback.OnReceive(message).ConfigureAwait(false);
                if (reply is Task<object> task)
                {
                    reply = await task.ConfigureAwait(false);
                }

                if (reply is ValueTask<object> vtask)
                {
                    reply = await vtask.ConfigureAwait(false);
                }

                if (reply != null)
                {
                    //将一个Rpc应答回复给远端
                    //将rpcID * -1，区分上行下行
                    Callback.Send(rpcID * -1, reply);
                }
            }
        }

        //public IMiniAwaitable<RpcResult> Send<RpcResult>(object message, object options = null)
        //{
        //    var (rpcID, source) = RpcCallbackPool.Regist(options);
        //    Callback.Send(rpcID, message);
        //    return source;
        //}
    }
}
