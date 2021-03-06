﻿using Megumin.Remote;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    /// <summary>
    /// rpc完成时方法签名
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public delegate void RpcCallback(object message, Exception exception);

    /// <summary>
    /// 更新Rpc结果，框架调用，协助处理Rpc封装
    /// </summary>
    public interface IRpcCallbackPool
    {
        /// <summary>
        /// 默认Rpc超时毫秒数
        /// </summary>
        int DefaultTimeout { get; set; }
        /// <summary>
        /// 注册一个rpc过程，并返回一个rpcID，后续可通过rpcID完成回调
        /// </summary>
        /// <param name="options">参数表</param>
        /// <typeparam name="RpcResult"></typeparam>
        /// <returns></returns>
        (int rpcID, IMiniAwaitable<(RpcResult result, Exception exception)> source) 
            Regist<RpcResult>(object options = null);
        /// <summary>
        /// 注册一个rpc过程，并返回一个rpcID，后续可通过rpcID完成回调
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="OnException"></param>
        /// <param name="options">参数表</param>
        /// <returns></returns>
        (int rpcID, IMiniAwaitable<RpcResult> source) 
            Regist<RpcResult>(Action<Exception> OnException, object options = null);
        /// <summary>
        /// 取得rpc回调函数
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="rpc"></param>
        /// <returns></returns>
        bool TryGetValue(int rpcID, out (DateTime startTime, RpcCallback rpcCallback) rpc);
        /// <summary>
        /// 取得rpc回调函数，并从rpc回调池中移除
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="rpc"></param>
        /// <returns></returns>
        bool TryDequeue(int rpcID, out (DateTime startTime, RpcCallback rpcCallback) rpc);
        /// <summary>
        /// 从rpc回调池中移除
        /// </summary>
        /// <param name="rpcID"></param>
        bool Remove(int rpcID);
        /// <summary>
        /// 触发rpc回调
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        bool TrySetResult(int rpcID, object msg);
        /// <summary>
        /// 触发rpc回调
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="exception"></param>
        /// <returns></returns>
        bool TrySetException(int rpcID, Exception exception);
    }

    /// <summary>
    /// Rpc回调注册池
    /// 每个session大约每秒30个包，超时时间默认为30秒；
    /// </summary>
    public class RpcCallbackPool : Dictionary<int, (DateTime startTime, RpcCallback rpcCallback)>,
        IRpcCallbackPool
    {
        int rpcCursor = 0;
        readonly object rpcCursorLock = new object();

        public RpcCallbackPool()
        {

        }

        public RpcCallbackPool(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// 默认30000ms
        /// </summary>
        public int DefaultTimeout { get; set; } = 30000;

        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为正数，回复的RpcID为负数，正负一一对应
        /// <para>0,int.MinValue 为无效值</para> 
        /// <seealso cref="RpcRemoteOld.DiversionProcess(int, short, int, object)"/>
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetRpcID()
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

        public (int rpcID, IMiniAwaitable<(RpcResult result, Exception exception)> source) 
            Regist<RpcResult>(object options = null)
        {
            int rpcID = GetRpcID();

            IMiniAwaitable<(RpcResult result, Exception exception)> source
                = MiniTask<(RpcResult result, Exception exception)>.Rent();
            var key = rpcID * -1;

            CheckKeyConflict(key);

            lock (dequeueLock)
            {
                this[key] = (DateTime.Now,
                    (resp, ex) =>
                    {
                        if (ex == null)
                        {
                            if (resp is RpcResult result)
                            {
                                source.SetResult((result, null));
                            }
                            else
                            {
                                if (resp == null)
                                {
                                    source.SetResult((default, new NullReferenceException()));
                                }
                                else
                                {
                                    ///转换类型错误
                                    source.SetResult((default,
                                        new InvalidCastException($"Return {resp.GetType()} type, cannot be converted to {typeof(RpcResult)}" +
                                        $"/返回{resp.GetType()}类型，无法转换为{typeof(RpcResult)}")));
                                }

                            }
                        }
                        else
                        {
                            source.SetResult((default, ex));
                        }

                        //todo
                        //source 回收
                    }
                );
            }

            CreateCheckTimeout<RpcResult>(key, options);

            return (rpcID, source);
        }

        /// <summary>
        /// rpcID冲突检查
        /// </summary>
        /// <param name="key"></param>
        void CheckKeyConflict(int key)
        {
            if (TryDequeue(key, out var callback))
            {
                ///如果出现RpcID冲突，认为前一个已经超时。
                callback.rpcCallback?.Invoke(null, new TimeoutException("RpcID overlaps and timeouts the previous callback/RpcID 重叠，对前一个回调进行超时处理"));
            }
        }

        public (int rpcID, IMiniAwaitable<RpcResult> source) 
            Regist<RpcResult>(Action<Exception> OnException, object options = null)
        {
            int rpcID = GetRpcID();
            IMiniAwaitable<RpcResult> source = MiniTask<RpcResult>.Rent();
            var key = rpcID * -1;

            CheckKeyConflict(key);

            lock (dequeueLock)
            {
                this[key] = (DateTime.Now,
                    (resp, ex) =>
                    {
                        if (ex == null)
                        {
                            if (resp is RpcResult result)
                            {
                                source.SetResult(result);
                            }
                            else
                            {
                                source.CancelWithNotExceptionAndContinuation();
                                if (resp == null)
                                {
                                    OnException?.Invoke(new NullReferenceException());
                                }
                                else
                                {
                                    ///转换类型错误
                                    OnException?.Invoke(new InvalidCastException($"Return {resp.GetType()} type, cannot be converted to {typeof(RpcResult)}" +
                                        $"/返回{resp.GetType()}类型，无法转换为{typeof(RpcResult)}"));
                                }
                            }
                        }
                        else
                        {
                            source.CancelWithNotExceptionAndContinuation();
                            OnException?.Invoke(ex);
                        }
                    }
                );
            }

            CreateCheckTimeout<RpcResult>(key, options);

            return (rpcID, source);
        }

        /// <summary>
        /// 创建超时检查
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="options"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateCheckTimeout<RpcResult>(int rpcID, object options)
        {
            int timeout = DefaultTimeout;
            if (options is IRpcTimeoutOption option)
            {
                timeout = option.MillisecondsDelay;
            }

            if (timeout >= 0)
            {
                CreateCheckTimeout(rpcID,typeof(RpcResult).FullName, timeout);
            }
        }

        /// <summary>
        /// 创建超时检查
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="resultTypeName"></param>
        /// <param name="timeOutMilliseconds"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateCheckTimeout(int rpcID,string resultTypeName, int timeOutMilliseconds)
        {
            ///备注：即使异步发送被同步调用，此处也不会发生错误。
            ///同步调用，当返回消息返回时，会从回调池移除，
            ///那么计时器结束时将不会找到Task。如果调用出没有保持Task引用，
            ///那么Task会成为孤岛，被GC回收。

            ///超时检查
            Task.Run(async () =>
            {
                if (timeOutMilliseconds >= 0)
                {
                    await Task.Delay(timeOutMilliseconds);
                    if (TryDequeue(rpcID, out var rpc))
                    {
                        MessageThreadTransducer.Invoke(() =>
                        {
                            rpc.rpcCallback?.Invoke(null, 
                                new TimeoutException($"The RPC [{rpcID}] callback timed out and did not get a remote response./RPC [{rpcID}] 回调超时，请求结果[{resultTypeName}],没有得到远端响应。"));
                        });
                    }
                }
            });
        }

        readonly object dequeueLock = new object();
        public bool TryDequeue(int rpcID, out (DateTime startTime, RpcCallback rpcCallback) rpc)
        {
            lock (dequeueLock)
            {
                if (TryGetValue(rpcID, out rpc))
                {
                    Remove(rpcID);
                    return true;
                }
            }

            return false;
        }

        public bool TrySetResult(int rpcID, object msg)
        {
            return TryComplate(rpcID, msg, null);
        }

        public bool TrySetException(int rpcID, Exception exception)
        {
            return TryComplate(rpcID, null, exception);
        }

        bool TryComplate(int rpcID, object msg,Exception exception)
        {
            ///rpc响应
            if (TryDequeue(rpcID, out var rpc))
            {
                rpc.rpcCallback?.Invoke(msg, exception);
                return true;
            }
            return false;
        }
    }
}
