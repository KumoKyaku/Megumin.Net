using Megumin.Message;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{


    /// <summary>
    /// Rpc回调注册池
    /// 每个session大约每秒30个包，超时时间默认为30秒；
    /// </summary>
    public class RpcCallbackPool : Dictionary<int, (DateTime startTime, RpcCallback rpcCallback)>, IRpcCallbackPool
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
        public int RpcTimeOutMilliseconds { get; set; } = 30000;
        delegate void RpcCallback(object message, Exception exception);
        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为正数，回复的RpcID为负数，正负一一对应
        /// <para>0,int.MinValue 为无效值</para> 
        /// <seealso cref="RemoteBase.Deal(int, object)"/>
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

        public (int rpcID, IMiniAwaitable<(RpcResult result, Exception exception)> source) Regist<RpcResult>()
        {
            int rpcID = GetRpcID();

            IMiniAwaitable<(RpcResult result, Exception exception)> source = MiniTask<(RpcResult result, Exception exception)>.Rent();
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

            CreateCheckTimeout(key);

            return (rpcID, source);
        }

        void CheckKeyConflict(int key)
        {
            if (TryDequeue(key, out var callback))
            {
                ///如果出现RpcID冲突，认为前一个已经超时。
                callback.rpcCallback?.Invoke(null, new TimeoutException("RpcID overlaps and timeouts the previous callback/RpcID 重叠，对前一个回调进行超时处理"));
            }
        }

        public (int rpcID, IMiniAwaitable<RpcResult> source) Regist<RpcResult>(Action<Exception> OnException)
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

            CreateCheckTimeout(key);

            return (rpcID, source);
        }

        /// <summary>
        /// 创建超时检查
        /// </summary>
        /// <param name="rpcID"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateCheckTimeout(int rpcID)
        {
            ///备注：即使异步发送被同步调用，此处也不会发生错误。
            ///同步调用，当返回消息返回时，会从回调池移除，
            ///那么计时器结束时将不会找到Task。如果调用出没有保持Task引用，
            ///那么Task会成为孤岛，被GC回收。

            ///超时检查
            Task.Run(async () =>
            {
                if (RpcTimeOutMilliseconds >= 0)
                {
                    await Task.Delay(RpcTimeOutMilliseconds);
                    if (TryDequeue(rpcID, out var rpc))
                    {
                        MessageThreadTransducer.Invoke(() =>
                        {
                            rpc.rpcCallback?.Invoke(null, new TimeoutException($"The RPC {rpcID} callback timed out and did not get a remote response./RPC {rpcID} 回调超时，没有得到远端响应。"));
                        });
                    }
                }
            });
        }

        readonly object dequeueLock = new object();
        public bool TryDequeue(int rpcID, out (DateTime startTime, Net.Remote.RpcCallback rpcCallback) rpc)
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

        void IRpcCallbackPool.Remove(int rpcID) => Remove(rpcID);

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
