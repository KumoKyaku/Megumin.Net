using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Net.Remote;

namespace System.Threading.Tasks
{
    /// <summary>
    /// 可异步等待的
    /// <para>不支持ContinueWith，建议将任何ContinueWith转化为await。ContinueWith的复杂度很高，我写不出绝对安全的实现。</para>
    /// https://www.codeproject.com/Articles/1018071/ContinueWith-Vs-await#
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMiniAwaitable<T>
    {
        ///实现需要处理多少情况
        ///1 异步调用  同步调用
        ///2 异步完成  同步完成
        ///3 成功完成  失败完成 （没有取消功能，只有超时）
        ///以上彼此正交
        ///异步调用 UnsafeOnCompleted  SetResult 先后调用顺序

        /// <summary>
        /// 
        /// </summary>
        bool IsCompleted { get; }
        /// <summary>
        /// 
        /// </summary>
        T Result { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="continuation"></param>
        void UnsafeOnCompleted(Action continuation);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="continuation"></param>
        void OnCompleted(Action continuation);
        /// <summary>
        /// 通过设定结果值触发后续方法
        /// </summary>
        /// <param name="result"></param>
        void SetResult(T result);
        /// <summary>
        /// 通过此方法结束一个await 而不触发后续方法，也不触发异常，并释放所有资源
        /// 主要针对某些时候持有Task,却不await
        /// </summary>
        void CancelWithNotExceptionAndContinuation();
    }

    public struct MiniTaskAwaiter<T> : ICriticalNotifyCompletion
    {
        private IMiniAwaitable<T> CanAwaiter;

        public bool IsCompleted => CanAwaiter.IsCompleted;

        public T GetResult()
        {
            return CanAwaiter.Result;
        }

        public MiniTaskAwaiter(IMiniAwaitable<T> canAwait)
        {
            this.CanAwaiter = canAwait;
        }
        public void UnsafeOnCompleted(Action continuation)
        {
            CanAwaiter.UnsafeOnCompleted(continuation);
        }

        public void OnCompleted(Action continuation)
        {
            CanAwaiter.OnCompleted(continuation);
        }
    }
}

public static class ICanAwaitableEx_D248AE7ECAD0420DAF1BCEA2801012FF
{
    public static MiniTaskAwaiter<T> GetAwaiter<T>(this IMiniAwaitable<T> canAwaitable)
    {
        return new MiniTaskAwaiter<T>(canAwaitable);
    }
}