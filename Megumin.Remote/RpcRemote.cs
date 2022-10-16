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
    public partial class RpcRemote : RemoteBase, IDealMessageable, IRemoteUID<int>, IRpcCallback<int>, IRemote
    {
        public virtual int UID { get; set; }
        public RpcLayer RpcLayer { get; set; } = new RpcLayer();

        public void SetTransport<T>(T transport) where T : BaseTransport, ITransportable
        {
            transport.RemoteCore = this;
            Transport = transport;
        }

        protected virtual async void ProcessRecevie(int rpcID, short cmd, int messageID, object message, object options = null)
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
        protected virtual async void DealRelay<T>(int rpcID, T reply)
        {
            if (reply != null)
            {
                if (reply is Task<T> task)
                {
                    reply = await task.ConfigureAwait(false);
                }

                if (reply is ValueTask<T> vtask)
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
        public override void DeserializeSuccess(int rpcID, short cmd, int messageID, object message, object options = null)
        {
            ///分流普通消息和RPC回复消息

            var post = UseThreadSchedule(rpcID, cmd, messageID, message);

            //第一版设计中先计算是否使用线程调度器，在分流。
            //后续设置Rpc过程是否使用线程调度器，增加发送处设置。
            //在超时抛出异常时，仍然需要决定是否转换线程，
            //所以现在先分流决定走Rpc流程还是Recevie流程，每个流程自己处理线程调度。

            //Rpc线程转换在RpcLayer 内部处理
            this.RpcLayer.TrySetUseThreadScheduleResult(rpcID, post);

            if (!RpcLayer.TryInput(rpcID, message))
            {
                ///在这里处理线程转换,是否将后续处理切换到<see cref="MessageThreadTransducer"/>线程中去.
                ///切换线程后调用 还是直接调用<see cref="ProcessRecevie"/>的区别
                if (post)
                {
                    Push2MessageThreadTransducer(rpcID, cmd, messageID, message, options);
                }
                else
                {
                    ProcessRecevie(rpcID, cmd, messageID, message, options);
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
        /// <param name="options"></param>
        /// <remarks>独立一个函数，不然<see cref="MessageThreadTransducer.Push(IDealMessageable, int, short, int, object,object)"/>继承者无法调用</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.DebuggerHidden]
        protected void Push2MessageThreadTransducer(int rpcID, short cmd, int messageID, object message, object options = null)
        {
            MessageThreadTransducer.Push(this, rpcID, cmd, messageID, message, options);
        }

        void IDealMessageable.Deal(int rpcID, short cmd, int messageID, object message, object options)
        {
            ProcessRecevie(rpcID, cmd, messageID, message, options);
        }

        public virtual void OnSendSafeAwaitException<T, Result>(T request,
                                                                   Result response,
                                                                   Action<Exception> onException,
                                                                   Exception finnalException)
        {
            onException?.Invoke(finnalException);
        }
    }

    public partial class RpcRemote : ISendAsyncable
    {
        public virtual ValueTask<(Result result, Exception exception)>
            SendAsync<T, Result>(T message, object options = null)
        {
            return RpcLayer.SendAsync<T, Result>(message, this, options);
        }

        public virtual ValueTask<Result> SendAsyncSafeAwait<T, Result>
            (T message, object options = null, Action<Exception> onException = null)
        {
            return RpcLayer.SendAsyncSafeAwait<T, Result>(message, this, options, onException);
        }

        public ValueTask<(Result result, Exception exception)>
            SendAsync<Result>(object message, object options = null)
        {
            return SendAsync<object, Result>(message, options);
        }

        public ValueTask<Result> SendAsyncSafeAwait<Result>
            (object message, object options = null, Action<Exception> onException = null)
        {
            return SendAsyncSafeAwait<object, Result>(message, options, onException);
        }
    }
}
