using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
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
    public abstract class RpcRemote2 : RemoteBase, IDealMessageable, ISendCanAwaitable
    {
        public ObjectRpcCallbackPool RpcCallbackPool { get; } = new ObjectRpcCallbackPool(31);

        /// <summary>
        /// 分流普通消息和RPC回复消息
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected async virtual void DiversionProcess(int rpcID, short cmd, int messageID, object message)
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
                var reply = await OnReceive(cmd, messageID, message);
                if (reply is Task<object> task)
                {
                    reply = await task;
                }

                if (reply is ValueTask<object> vtask)
                {
                    reply = await vtask;
                }

                if (reply != null)
                {
                    //将一个Rpc应答回复给远端
                    //将rpcID * -1，区分上行下行
                    Send(rpcID * -1, reply);
                }
            }
        }

        protected override void DeserializeSuccess(int rpcID, short cmd, int messageID, object message)
        {
            var trans = UseThreadSchedule(rpcID, cmd, messageID, message);
            if (trans)
            {
                Push2MessageThreadTransducer(rpcID, cmd, messageID, message);
            }
            else
            {
                DiversionProcess(rpcID, cmd, messageID, message);
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
        protected void Push2MessageThreadTransducer(int rpcID, short cmd, int messageID, object message)
        {
            MessageThreadTransducer.Push(rpcID, cmd, messageID, message, this);
        }

        void IDealMessageable.Deal(int rpcID, short cmd, int messageID, object message)
        {
            DiversionProcess(rpcID, cmd, messageID, message);
        }

        public virtual async ValueTask<(RpcResult result, Exception exception)>
            Send<RpcResult>(object message, object options = null)
        {
            //可以在这里重写异常堆栈信息。
            //StackTrace stackTrace = new System.Diagnostics.StackTrace();
            (object resp, Exception ex) = await InnerRpcSend(message, options);
            return ValidResult<RpcResult>(message, resp, ex, options);
        }

        /// <summary>
        /// 验证resp空引用和返回类型,补充和转化异常
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="request"></param>
        /// <param name="resp"></param>
        /// <param name="ex"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual (RpcResult result, Exception exception)
            ValidResult<RpcResult>(object request,
                                   object resp,
                                   Exception ex,
                                   object options = null)
        {
            RpcResult result = default;
            if (ex == null)
            {
                if (resp is RpcResult castedValue)
                {
                    result = castedValue;
                }
                else
                {
                    if (resp == null)
                    {
                        ex = new NullReferenceException();
                    }
                    else
                    {
                        ///转换类型错误
                        ex = new InvalidCastException($"Return {resp.GetType()} type, cannot be converted to {typeof(RpcResult)}" +
                            $"/返回{resp.GetType()}类型，无法转换为{typeof(RpcResult)}");
                    }

                }
            }
            else
            {
                if (ex is RcpTimeoutException timeout)
                {
                    timeout.RequstType = request.GetType();
                    timeout.ResponseType = typeof(RpcResult);
                }
            }

            return (result, ex);
        }

        public virtual async ValueTask<RpcResult> SendSafeAwait<RpcResult>
            (object message, Action<Exception> onException = null, object options = null)
        {
            var (tempresp, tempex) = await InnerRpcSend(message, options);

            IMiniAwaitable<RpcResult> tempsource = MiniTask<RpcResult>.Rent();

            var validResult = ValidResult<RpcResult>(message, tempresp, tempex, options);

            if (validResult.exception == null)
            {
                tempsource.SetResult(validResult.result);
            }
            else
            {
                //取消异步后续，转为调用OnException
                tempsource.CancelWithNotExceptionAndContinuation();
                OnSendSafeAwaitException(message, tempresp, onException, validResult.exception);
            }

            var result = await tempsource;
            return result;
        }

        /// <summary>
        ///  <see cref="SendSafeAwait{RpcResult}(object, Action{Exception}, object)"/>收到obj response后，如果是异常，处理异常的逻辑。
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="onException"></param>
        /// <param name="finnalException"></param>
        protected virtual void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            onException?.Invoke(finnalException);
        }

        /// <summary>
        /// 内部Rpc发送，泛型在这一步转为非泛型。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual IMiniAwaitable<(object result, Exception exception)> InnerRpcSend(object message, object options = null)
        {
            var (rpcID, source) = RpcCallbackPool.Regist(options);

            try
            {
                Send(rpcID, message, options);
                return source;
            }
            catch (Exception e)
            {
                RpcCallbackPool.TrySetException(rpcID, e);
                return source;
            }
        }
    }
}
