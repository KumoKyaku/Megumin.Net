using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote.Rpc
{
    public interface IRpcCallback<in K>
    {
        void Send<T>(K rpcID, T message, object options = null);

        /// <summary>
        ///  <see cref="ISendCanAwaitable.SendSafeAwait{Result}(object, object, Action{Exception})"/>收到obj response后，如果是异常，处理异常的逻辑。
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="onException"></param>
        /// <param name="finnalException"></param>
        void OnSendSafeAwaitException<T, Result>(T request, Result response, Action<Exception> onException, Exception finnalException);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <remarks>
    /// <para/>Q:为什么用IMiniAwaitable 而不是ValueTask?
    /// <para/>A:开始时这个类直接和Send耦合，需要返回值一致，现在没有修改必要。性能要比ValueTask高那么一丁点。
    /// </remarks>
    public partial class RpcLayer :
        RpcCallbackPool<int, object, (int rpcID, IMiniAwaitable<(object result, Exception exception)>)>
    {
        int rpcCursor = 0;
        readonly object rpcCursorLock = new object();

        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为正数，回复的RpcID为负数，正负一一对应
        /// <para>0,int.MinValue 为无效值</para> 
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override int GetRpcID()
        {
            lock (rpcCursorLock)
            {
                if (rpcCursor == int.MaxValue)
                {
                    rpcCursor = 1;
                }
                else
                {
                    rpcCursor++;
                }

                return rpcCursor;
            }
        }

        public override (int rpcID, IMiniAwaitable<(object result, Exception exception)>) Regist(object options = null)
        {
            var source = MiniTask<(object result, Exception exception)>.Rent();
            var rpcID = Regist(source.SetResult, options);
            return (rpcID, source);
        }
    }

    /// <summary>
    /// 独立的Rpc层
    /// </summary>
    public partial class RpcLayer
    {
        /// <summary>
        /// 如果rpcID为负数，是rpc返回回复，返回true,此消息由RpcLayer处理。
        /// <para> 否则返回false，RpcLayer忽略此消息。</para>
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryInput(int rpcID, object message)
        {
            if (rpcID < 0)
            {
                //这个消息是rpc返回（回复的RpcID为负数）
                TrySetResult(rpcID * -1, message);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 验证resp空引用和返回类型,补充和转化异常
        /// </summary>
        /// <typeparam name="Result"></typeparam>
        /// <param name="request"></param>
        /// <param name="resp"></param>
        /// <param name="ex"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual (Result result, Exception exception)
            ValidResult<Result>(object request,
                                   object resp,
                                   Exception ex,
                                   object options = null)
        {
            Result result = default;
            if (ex == null)
            {
                if (resp is Result castedValue)
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
                        ex = new InvalidCastException($"Return {resp.GetType()} type, cannot be converted to {typeof(Result)}" +
                            $"/返回{resp.GetType()}类型，无法转换为{typeof(Result)}");
                    }

                }
            }
            else
            {
                if (ex is RcpTimeoutException timeout)
                {
                    timeout.RequstType = request.GetType();
                    timeout.ResponseType = typeof(Result);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual IMiniAwaitable<(object result, Exception exception)>
            InnerRpcSend<T>(T message, IRpcCallback<int> callback, object options = null)
        {
            var (rpcID, source) = Regist(options);

            try
            {
                callback.Send(rpcID, message, options);
                return source;
            }
            catch (Exception e)
            {
                TrySetException(rpcID, e);
                return source;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual async ValueTask<(Result result, Exception exception)>
            Send<T, Result>(T message, IRpcCallback<int> callback, object options = null)
        {
            //可以在这里重写异常堆栈信息。
            //StackTrace stackTrace = new System.Diagnostics.StackTrace();
            (object resp, Exception ex) = await InnerRpcSend(message, callback, options);

            //这里是TaskPool线程或者MessageThreadTransducer线程

            return ValidResult<Result>(message, resp, ex, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual async ValueTask<Result> SendSafeAwait<T, Result>
             (T message, IRpcCallback<int> callback, object options = null, Action<Exception> onException = null)
        {
            var (tempresp, tempex) = await InnerRpcSend(message, callback, options);

            //这里是TaskPool线程或者MessageThreadTransducer线程
            var validResult = ValidResult<Result>(message, tempresp, tempex, options);

            //这里使用tempsource,来达到出现异常取消异步后续的目的

            ////相当于一个TaskCompletionSource实例
            //TaskCompletionSource<Result> source = new TaskCompletionSource<Result>();
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

            IMiniAwaitable<Result> tempsource = MiniTask<Result>.Rent();

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
