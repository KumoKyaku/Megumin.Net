using Megumin.Remote.Rpc;
using Net.Remote;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    /// <summary>
    /// 支持Rpc功能的
    /// <para/>优化了发送逻辑，使用一个异步模式取代了一个底层泛型。
    /// <para/>Rpcpool类型可以确定，提高了效率。
    /// <para/>层次划分更加明确。
    /// <para/>
    /// 没有设计成扩展函数或者静态函数是方便子类重写。
    /// </summary>
    /// <remarks>一些与RPC支持相关的函数写在这里。</remarks>
    public abstract class RpcRemote : RemoteBase, IDealMessageable, ISendCanAwaitable, IRemoteUID<int>, IRpcCallback<int>
    {
        public virtual int UID { get; set; }
        public RpcLayer RpcLayer = new RpcLayer();

        protected virtual async void ProcessRecevie(int rpcID, short cmd, int messageID, object message)
        {
            //这里封装起来OnReceive故意隐藏rpcID，就是让上层忽略rpc细节。
            //如果有特殊需求，就重写这个方法。

            //这个消息非Rpc返回

            //普通响应

            object reply = null;
            reply = await PreReceive(cmd, messageID, message, out var stopReceive);

            //DealRelay(rpcID, reply);
            if (reply != null)
            {
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
                    Send(rpcID * -1, reply);
                }
            }

            if (!stopReceive)
            {
                reply = await OnReceive(cmd, messageID, message).ConfigureAwait(false);

                //DealRelay(rpcID, reply);
                if (reply != null)
                {
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
                        Send(rpcID * -1, reply);
                    }
                }
            }
        }

        /// <summary>
        /// 手动内联，少一个异步方法可以节省一些开销。避免生成异步状态机等，可以与DiversionProcess 合成一个。
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="reply"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual async void DealRelay(int rpcID, object reply)
        {
            if (reply != null)
            {
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
                    Send(rpcID * -1, reply);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void DeserializeSuccess(int rpcID, short cmd, int messageID, object message)
        {
            ///分流普通消息和RPC回复消息

            var post = UseThreadSchedule(rpcID, cmd, messageID, message);

            //第一版设计中先计算是否使用线程调度器，在分流。
            //后续设置Rpc过程是否使用线程调度器，增加发送处设置。
            //在超时抛出异常时，仍然需要决定是否转换线程，
            //所以现在先分流决定走Rpc流程还是Recevie流程，每个流程自己处理线程调度。

            //Rpc线程转换在RpcLayer 内部处理
            this.RpcLayer.RpcCallbackPool.TrySetUseThreadScheduleResult(rpcID, post);

            if (!RpcLayer.TryInput(rpcID, message))
            {
                ///在这里处理线程转换,是否将后续处理切换到<see cref="MessageThreadTransducer"/>线程中去.
                ///切换线程后调用 还是直接调用<see cref="ProcessRecevie"/>的区别
                if (post)
                {
                    Push2MessageThreadTransducer(rpcID, cmd, messageID, message);
                }
                else
                {
                    ProcessRecevie(rpcID, cmd, messageID, message);
                }
            }
        }

        /// <summary>
        /// 推到线程转化器中
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <remarks>独立一个函数，不然<see cref="MessageThreadTransducer.Push(int, short, int, object, IDealMessageable)"/>继承者无法调用</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.DebuggerHidden]
        protected void Push2MessageThreadTransducer(int rpcID, short cmd, int messageID, object message)
        {
            MessageThreadTransducer.Push(rpcID, cmd, messageID, message, this);
        }

        void IDealMessageable.Deal(int rpcID, short cmd, int messageID, object message)
        {
            ProcessRecevie(rpcID, cmd, messageID, message);
        }

        public virtual ValueTask<(RpcResult result, Exception exception)>
            Send<RpcResult>(object message, object options = null)
        {
            return RpcLayer.Send<RpcResult>(message, this, options);
        }

        public virtual ValueTask<RpcResult> SendSafeAwait<RpcResult>
            (object message, Action<Exception> onException = null, object options = null)
        {
            return RpcLayer.SendSafeAwait<RpcResult>(message, this, onException, options);
        }

        public virtual void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            onException?.Invoke(finnalException);
        }
    }
}
