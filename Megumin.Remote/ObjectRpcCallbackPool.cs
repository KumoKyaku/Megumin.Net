using Megumin.Remote;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RpcSource = System.Threading.Tasks.IMiniAwaitable<(object result, System.Exception exception)>;

namespace Megumin.Remote
{
    /// <summary>
    /// Rpc回调注册池
    /// 每个session大约每秒30个包，超时时间默认为30秒；
    /// </summary>
    public class ObjectRpcCallbackPool : Dictionary<int, (DateTime startTime, RpcSource source)>
    {
        int rpcCursor = 0;
        readonly object rpcCursorLock = new object();

        public ObjectRpcCallbackPool()
        {

        }

        public ObjectRpcCallbackPool(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// 默认30000ms
        /// </summary>
        public int DefaultTimeout { get; set; } = 30000;

        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为正数，回复的RpcID为负数，正负一一对应
        /// <para>0,int.MinValue 为无效值</para> 
        /// <seealso cref="RpcRemote.DiversionProcess(int, short, int, object)"/>
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
        /// <summary>
        /// rpcID冲突检查
        /// </summary>
        /// <param name="key"></param>
        void CheckKeyConflict(int key)
        {
            if (TryDequeue(key, out var callback))
            {
                MessageThreadTransducer.Invoke(() =>
                {
                    //如果出现RpcID冲突，认为前一个已经超时。
                    callback.source?.SetResult((null, new TimeoutException("RpcID overlaps and timeouts the previous callback/RpcID 重叠，对前一个回调进行超时处理")));
                });
            }
        }

        public (int rpcID, RpcSource source) Regist(object options = null)
        {
            int rpcID = GetRpcID();

            RpcSource source = MiniTask<(object result, Exception exception)>.Rent();

            CheckKeyConflict(rpcID);

            lock (dequeueLock)
            {
                this[rpcID] = (DateTime.Now, source);
            }

            CreateCheckTimeout(rpcID, options);
            return (rpcID, source);
        }

        /// <summary>
        /// 创建超时检查
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="options"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateCheckTimeout(int rpcID, object options)
        {
            int timeout = DefaultTimeout;
            if (options is IRpcTimeoutOption option)
            {
                timeout = option.MillisecondsDelay;
            }

            if (timeout >= 0)
            {
                CreateCheckTimeout(rpcID, timeout);
            }
        }

        /// <summary>
        /// 创建超时检查
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="timeOutMilliseconds"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        async void CreateCheckTimeout(int rpcID, int timeOutMilliseconds)
        {
            ///备注：即使异步发送被同步调用，此处也不会发生错误。
            ///同步调用，当返回消息返回时，会从回调池移除，
            ///那么计时器结束时将不会找到Task。如果调用出没有保持Task引用，
            ///那么Task会成为孤岛，被GC回收。

            ///超时检查
            if (timeOutMilliseconds >= 0)
            {
                await Task.Delay(timeOutMilliseconds);
                MessageThreadTransducer.Invoke(() =>
                {
                    TrySetException(rpcID, new RcpTimeoutException());
                });
            }
        }

        readonly object dequeueLock = new object();
        public bool TryDequeue(int rpcID, out (DateTime startTime, RpcSource source) rpc)
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

        bool TryComplate(int rpcID, object msg, Exception exception)
        {
            ///rpc响应
            if (TryDequeue(rpcID, out var rpc))
            {
                rpc.source.SetResult((msg, exception));
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// rpc超时异常
    /// </summary>
    public class RcpTimeoutException : TimeoutException
    {
        public override string Message => MyMessage + base.Message;

        public Type RequstType { get; internal set; }
        public Type ResponseType { get; internal set; }

        string MyMessage
        {
            get
            {
                return $"Rpc请求[{RequstType?.Name}]超时，请求结果类型[{ResponseType?.Name}]";
            }
        }
    }
}
