using System;
using System.Collections.Generic;
using System.Text;
using Net.Remote;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks
{
    /// <summary>
    /// 一个简单异步任务实现，特点是缓存任务不构造任务实例。
    /// <para>不支持保持线程上下文功能,也就是异步后续的线程为<see cref="SetResult(T)"/>线程.</para>
    /// 如果任务没有完成访问<see cref="Result"/>,会返回null而不是阻塞。这与Task不一致.
    /// todo 使用 IValueTaskSource 优化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MiniTask<T> : IMiniAwaitable<T>
    {
        enum State
        {
            InPool,
            Waiting,
            Success,
            /// <summary>
            /// 取消的
            /// </summary>
            Canceled,
        }

        /// <summary>
        /// 
        /// </summary>
        public static int MaxCount { get; set; } = 512;

        static ConcurrentQueue<MiniTask<T>> pool = new ConcurrentQueue<MiniTask<T>>();
        public static MiniTask<T> Rent()
        {
            if (pool.TryDequeue(out var task))
            {
                if (task != null)
                {
                    task.state = State.Waiting;
                    return task;
                }
            }

            return new MiniTask<T>() { state = State.Waiting };
        }

        public static void ClearPool()
        {
            lock (pool)
            {
                while (pool.Count > 0)
                {
                    pool.TryDequeue(out var task);
                    task?.Reset();
                }
            }
        }

        volatile State state = State.InPool;

        private Action continuation;
        /// <summary>
        /// 是否进入异步挂起阶段
        /// </summary>
        private bool alreadyEnterAsync = false;


        /// <inheritdoc/>
        /// <remarks><see cref="State.Canceled"/> 不能算完成状态，
        /// <para></para>否则 先CancelWithNotExceptionAndContinuation，之后await 
        /// <para></para>会触发异步状态机同步完成，会直接取结果，进行异步后续，无法起到中断效果。
        /// <para></para>一定要 进入if (!awaiter.IsCompleted) 分支走一下UnsafeOnCompleted 触发回池。后续优化</remarks>
        public bool IsCompleted => state == State.Success;
        /// <summary>
        /// 请不要同步访问Result。即使同步完成也应该使用await 关键字。同步访问可能无法取得正确的值，或抛出异常。
        /// </summary>
        public T Result { get; protected set; }
        readonly object innerlock = new object();

        public void UnsafeOnCompleted(Action continuation)
        {
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    ///这里被触发一定是是类库BUG。
                    throw new ArgumentException($"{nameof(MiniTask<T>)} task conflict, underlying error, please contact the framework author." +
                        $"/{nameof(MiniTask<T>)}任务冲突，底层错误，请联系框架作者。");
                }

                alreadyEnterAsync = true;
                this.continuation -= continuation;
                this.continuation += continuation;
                TryComplete();
            }
        }

        /// <summary>
        /// 无需实现
        /// </summary>
        /// <param name="continuation"></param>
        public void OnCompleted(Action continuation)
        {
            //lock (innerlock)
            //{
            //    if (state == State.InPool)
            //    {
            //        ///这里被触发一定是是类库BUG。
            //        throw new ArgumentException($"{nameof(MiniTask<T>)} task conflict, underlying error, please contact the framework author." +
            //            $"/{nameof(MiniTask<T>)}任务冲突，底层错误，请联系框架作者。");
            //    }

            //    alreadyEnterAsync = true;
            //    this.continuation -= continuation;
            //    this.continuation += continuation;
            //    TryComplete();
            //}
            throw new NotImplementedException();
        }

        public void SetResult(T result)
        {
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    throw new InvalidOperationException($"Task does not exist/任务不存在");
                }
                this.Result = result;
                state = State.Success;
                TryComplete();
            }
        }

        private void TryComplete()
        {
            if (alreadyEnterAsync)
            {
                if (state == State.Waiting)
                {
                    return;
                }

                if (state == State.Success)
                {
                    continuation?.Invoke();
                }

                ///处理后续方法结束，归还到池中
                this.Return();
            }
        }

        public void CancelWithNotExceptionAndContinuation()
        {
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    throw new InvalidOperationException($"Task does not exist/任务不存在");
                }

                Result = default;
                state = State.Canceled;
                TryComplete();
            }
        }

#if DEBUG
        int lastThreadID;
#endif

        void Return()
        {
            Reset();

            if (state != State.InPool)
            {
                ///state = State.InPool;必须在pool.Enqueue(this);之前。
                ///因为当pool为空的时候，放入池的元素会被立刻取出。并将状态设置为Waiting。
                ///如果state = State.InPool;在pool.Enqueue(this)后，那么会导致Waiting 状态被错误的设置为InPool;
                /// **** 我在这里花费了4个小时（sad）。
                state = State.InPool;

#if DEBUG
                lastThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif

                if (pool.Count < MaxCount)
                {
                    pool.Enqueue(this);
                }
            }
        }

        void Reset()
        {
            alreadyEnterAsync = false;
            Result = default;
            continuation = null;
        }

        //[StackTraceHidden]
        public void ValidateEnd()
        {
            //todo  
            //if (IsCompleted == false)
            //{
            //    //此处应该阻塞线程,当完成时释放.暂不实现,使用者自行注意.
            //    throw new NotImplementedException("异步获取结果错误");
            //}
        }
    }
}
