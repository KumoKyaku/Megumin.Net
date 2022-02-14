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
    public interface IMiniAwaitable
    {
        ///实现需要处理多少情况
        ///1 异步调用  同步调用
        ///2 异步完成  同步完成
        ///3 成功完成  失败完成 （没有取消功能，只有超时）
        ///以上彼此正交
        ///异步调用 UnsafeOnCompleted  SetResult 先后调用顺序
        ///记住 await 时才调用 UnsafeOnCompleted，认为 await == UnsafeOnCompleted == 将下文代码包装成回调函数注册到Task中即可。
        ///async == AsyncTaskMethodBuilder.Create().Task,并在方法末尾SetResult。async是隐藏的生成一个Task/ValueTask。

        /// <summary>
        /// 
        /// </summary>
        bool IsCompleted { get; }
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
        /// 通过此方法结束一个await 而不触发后续方法，也不触发异常，并释放所有资源
        /// 主要针对某些时候持有Task,却不await
        /// </summary>
        void CancelWithNotExceptionAndContinuation();

        /// <summary>
        /// 验证是否完成,在GetResult时调用,应该保证如果未完成时阻塞.
        /// </summary>
        //[StackTraceHidden]
        void ValidateEnd();
    }

    public struct MiniTaskAwaiter : ICriticalNotifyCompletion, INotifyCompletion
    {
        private IMiniAwaitable CanAwaiter;

        /// <summary>
        /// 
        /// </summary>
        public bool IsCompleted => CanAwaiter.IsCompleted;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void GetResult()
        {
            //TODO,缺少安全验证,但通常不会出bug.
            CanAwaiter.ValidateEnd();
        }

        public MiniTaskAwaiter(IMiniAwaitable canAwait)
        {
            this.CanAwaiter = canAwait;
        }
        /// <summary>
        /// 当没有同步完成时，向CanAwaiter注册回调，CanAwaite会将回调保存起来，用于在完成时调用。
        /// </summary>
        /// <param name="continuation"></param>
        public void UnsafeOnCompleted(Action continuation)
        {
            CanAwaiter.UnsafeOnCompleted(continuation);
        }

        public void OnCompleted(Action continuation)
        {
            CanAwaiter.OnCompleted(continuation);
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMiniAwaitable<T> : IMiniAwaitable
    {
        /// <summary>
        /// 
        /// </summary>
        T Result { get; }
        /// <summary>
        /// 通过设定结果值触发后续方法
        /// </summary>
        /// <param name="result"></param>
        void SetResult(T result);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct MiniTaskAwaiter<T> : ICriticalNotifyCompletion, INotifyCompletion
    {
        private IMiniAwaitable<T> CanAwaiter;

        /// <summary>
        /// 
        /// </summary>
        public bool IsCompleted => CanAwaiter.IsCompleted;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public T GetResult()
        {
            //TODO,缺少安全验证,但通常不会出bug.
            CanAwaiter.ValidateEnd();

            return CanAwaiter.Result;
        }

        public MiniTaskAwaiter(IMiniAwaitable<T> canAwait)
        {
            this.CanAwaiter = canAwait;
        }
        /// <summary>
        /// 当没有同步完成时，向CanAwaiter注册回调，CanAwaite会将回调保存起来，用于在完成时调用。
        /// </summary>
        /// <param name="continuation"></param>
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

/// <summary>
/// 
/// </summary>
public static class ICanAwaitableEx_D248AE7ECAD0420DAF1BCEA2801012FF
{

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="canAwaitable"></param>
    /// <returns></returns>
    public static MiniTaskAwaiter GetAwaiter(this IMiniAwaitable canAwaitable)
    {
        return new MiniTaskAwaiter(canAwaitable);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="canAwaitable"></param>
    /// <returns></returns>
    public static MiniTaskAwaiter<T> GetAwaiter<T>(this IMiniAwaitable<T> canAwaitable)
    {
        return new MiniTaskAwaiter<T>(canAwaitable);
    }
}



