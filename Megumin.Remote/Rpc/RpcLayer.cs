using Net.Remote;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote.Rpc
{
    public interface IRpcCallback<in K>
    {
        void Send(K rpcID, object message, object options = null);
        /// <summary>
        ///  <see cref="ISendCanAwaitable.SendSafeAwait{RpcResult}(object, object, Action{Exception})"/>收到obj response后，如果是异常，处理异常的逻辑。
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="onException"></param>
        /// <param name="finnalException"></param>
        void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException);
    }

    /// <summary>
    /// 独立的Rpc层
    /// </summary>
    public class RpcLayer
    {
        public ObjectRpcCallbackPool RpcCallbackPool { get; } = new ObjectRpcCallbackPool();

        /// <summary>
        /// 如果rpcID为负数，是rpc返回回复，返回true,此消息由RpcLayer处理。
        /// <para> 否则返回false，RpcLayer忽略此消息。</para>
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool TryInput(int rpcID, object message)
        {
            if (rpcID < 0)
            {
                //这个消息是rpc返回（回复的RpcID为负数）
                RpcCallbackPool?.TrySetResult(rpcID * -1, message);
                return true;
            }
            return false;
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

        /// <summary>
        /// 内部Rpc发送，泛型在这一步转为非泛型。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>
        /// 异步后续调用TaskPool线程或者MessageThreadTransducer线程,
        /// <see cref="RpcCallbackPool{K, M, A}.TrySetResult(K, M)"/>
        /// <see cref="MiniTask{T}.SetResult(T)"/>
        /// </remarks>
        protected virtual IMiniAwaitable<(object result, Exception exception)>
            InnerRpcSend(object message, IRpcCallback<int> callback, object options = null)
        {
            var (rpcID, source) = RpcCallbackPool.Regist(options);

            try
            {
                callback.Send(rpcID, message, options);
                return source;
            }
            catch (Exception e)
            {
                RpcCallbackPool.TrySetException(rpcID, e);
                return source;
            }
        }

        public virtual async ValueTask<(RpcResult result, Exception exception)>
            Send<RpcResult>(object message, IRpcCallback<int> callback, object options = null)
        {
            //可以在这里重写异常堆栈信息。
            //StackTrace stackTrace = new System.Diagnostics.StackTrace();
            (object resp, Exception ex) = await InnerRpcSend(message, callback, options);

            //这里是TaskPool线程或者MessageThreadTransducer线程

            return ValidResult<RpcResult>(message, resp, ex, options);
        }

        public virtual async ValueTask<RpcResult> SendSafeAwait<RpcResult>
             (object message, IRpcCallback<int> callback, object options = null, Action<Exception> onException = null)
        {
            var (tempresp, tempex) = await InnerRpcSend(message, callback, options);

            //这里是TaskPool线程或者MessageThreadTransducer线程
            var validResult = ValidResult<RpcResult>(message, tempresp, tempex, options);

            //这里使用tempsource,来达到出现异常取消异步后续的目的

            ////相当于一个TaskCompletionSource实例
            //TaskCompletionSource<RpcResult> source = new TaskCompletionSource<RpcResult>();
            //if (validResult.exception == null)
            //{
            //    source.SetResult(validResult.result);
            //}
            //else
            //{
            //    //取消异步后续，转为调用OnException
            //    //source什么也不做
            //    OnSendSafeAwaitException(message, tempresp, onException, validResult.exception);
            //}

            //return await source.Task;

            IMiniAwaitable<RpcResult> tempsource = MiniTask<RpcResult>.Rent();

            if (validResult.exception == null)
            {
                tempsource.SetResult(validResult.result);
            }
            else
            {
                //取消异步后续，转为调用OnException
                tempsource.CancelWithNotExceptionAndContinuation();
                callback.OnSendSafeAwaitException(message, tempresp, onException, validResult.exception);
            }

            var result = await tempsource;
            return result;
        }
    }
}
