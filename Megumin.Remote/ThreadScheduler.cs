using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DealWorkQueue = System.Collections.Concurrent.ConcurrentQueue<Megumin.Remote.DealWork>;
using RequestWorkQueue = System.Collections.Concurrent.ConcurrentQueue<Megumin.Remote.RequestWork>;

namespace Megumin.Remote
{
    /// <summary>
    /// object消息 消费者接口
    /// </summary>
    public interface IObjectMessageReceiver
    {
        /// <summary>
        /// 处理消息实例,并返回一个可等待结果
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        ValueTask<object> ObjectMessageReceive(int rpcID, short cmd, int messageID, object message);
    }

    /// <summary>
    /// 处理object消息 消费者接口
    /// </summary>
    public interface IDealMessageable
    {
        /// <summary>
        /// 处理消息实例
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        void Deal(int rpcID, short cmd, int messageID, object message, object options = null);
    }

    /// <summary>
    ///  处理object消息 消费者接口
    /// </summary>
    /// <typeparam name="HD">报头类型</typeparam>
    public interface IDealMessageable<HD>
    {
        /// <summary>
        /// 处理消息实例
        /// </summary>
        /// <param name="header"></param>
        /// <param name="message"></param>
        void Deal(in HD header, object message);
    }

    [Obsolete("设计缺陷,线程转换不应该带有异步逻辑,严重增加复杂性", true)]
    internal struct RequestWork
    {
        readonly int rpcID;
        readonly short cmd;
        readonly int messageID;
        readonly MiniTask<object> task;
        readonly object message;
        readonly IObjectMessageReceiver r;

        internal RequestWork(MiniTask<object> task, int rpcID, short cmd, int messageID,
            object message, IObjectMessageReceiver r)
        {
            this.rpcID = rpcID;
            this.cmd = cmd;
            this.messageID = messageID;
            this.task = task;
            this.message = message;
            this.r = r;
        }

        public async void Invoke()
        {
            if (this.task == null)
            {
                return;
            }
            ///此处可以忽略异常处理
            ///
            var response = await r.ObjectMessageReceive(rpcID, cmd, messageID, message);

            if (response is Task<object> task)
            {
                response = await task;
            }

            if (response is ValueTask<object> vtask)
            {
                response = await vtask;
            }

            this.task.SetResult(response);
        }
    }

    internal struct DealWork
    {
        readonly IDealMessageable r;
        readonly int rpcID;
        readonly short cmd;
        readonly int messageID;
        readonly object message;
        readonly object options;

        internal DealWork(IDealMessageable r, int rpcID, short cmd,
            int messageID, object message, object options)
        {
            this.rpcID = rpcID;
            this.cmd = cmd;
            this.messageID = messageID;
            this.message = message;
            this.r = r;
            this.options = options;
        }

        public void Invoke()
        {
            r.Deal(rpcID, cmd, messageID, message, options);
        }
    }

    public interface IThreadScheduler
    {
        void Invoke(Action action);
    }


    /// <summary>
    /// 消息线程调度器。
    /// 不使用异步，而是用回调函数的原因有3个。
    /// 1.异步会导致闭包不可控。不能针对性优化。还是有一定性能开销的。
    /// 2.异步的await关键字执行时机不确定。可能会导致消息顺序发生变化。有可能被错误使用，只有拿到Task立刻await才能保证正确。
    ///     例如可能拿到线程切换的task后，不进行await，而是保存起来，在后面的某个时间点await，这样不能正常工作。
    /// 3.异步的异常处理，异常传递比较麻烦，会引出更多的问题。多个并列的异步可能耦合在同一次回调里。异常后可能导致消息丢失。
    ///     例如这一帧有10个消息，都通过await附加到同一个回调委托中，但是中间某一个消息处理时出现异常，那么后面的消息可能就丢了。
    /// </summary>
    public partial class ThreadScheduler : IThreadScheduler
    {
        public static readonly ThreadScheduler Default = new();

        readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();
        [Obsolete("设计缺陷,线程转换不应该带有异步逻辑,严重增加复杂性", true)]
        readonly RequestWorkQueue requestWorkQueue = new RequestWorkQueue();
        readonly DealWorkQueue dealWorkQueue = new DealWorkQueue();
        //static readonly ThreadSwitcher DefaultSwitcher = new ThreadSwitcher();
        readonly ConcurrentQueue<MiniTask<int>> MiniTasksSwitcher = new ConcurrentQueue<MiniTask<int>>();
        readonly List<MiniTask<int>> MiniTaskNoAwait = new List<MiniTask<int>>();

        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        public void Update(double delta)
        {
            //while (requestWorkQueue.TryDequeue(out var res))
            //{
            //    res.Invoke();
            //}

            while (dealWorkQueue.TryDequeue(out var res))
            {
                res.Invoke();
            }

            while (actions.TryDequeue(out var callback))
            {
                callback?.Invoke();
            }

            //DefaultSwitcher.Tick();

            try
            {
                MiniTaskNoAwait.Clear();
                while (MiniTasksSwitcher.TryDequeue(out var miniTask))
                {
                    //保证线程切换后续已经await
                    if (miniTask.AlreadyEnterAsync)
                    {
                        miniTask.SetResult(0);
                    }
                    else
                    {
                        MiniTaskNoAwait.Add(miniTask);
                    }
                }
            }
            finally
            {
                //防止回调丢失
                foreach (var item in MiniTaskNoAwait)
                {
                    MiniTasksSwitcher.Enqueue(item);
                }
                MiniTaskNoAwait.Clear();
            }
        }

        /// <summary>
        /// 切换线程后的回调函数实际上就是IObjectMessageReceiver,既然可设置回调函数,就没有必要在有一个异步返回值.
        /// <para>将需要的异步操作都封装到 IObjectMessageReceiver</para>
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <param name="r"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [Obsolete("设计缺陷,线程转换不应该带有异步逻辑,严重增加复杂性", true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IMiniAwaitable<object> Push(int rpcID, short cmd, int messageID, object message, IObjectMessageReceiver r)
        {
            if (r == null)
            {
                throw new ArgumentNullException();
            }

            //这里是性能敏感区域，使用结构体优化，不使用action闭包
            MiniTask<object> task = MiniTask<object>.Rent();
            RequestWork work = new RequestWork(task, rpcID, cmd, messageID, message, r);
            requestWorkQueue.Enqueue(work);
            return task;
        }

        /// <summary>
        /// 专用函数,比<see cref="Switch"/>性能高,但是通用性不好
        /// </summary>
        /// <param name="r"></param>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(IDealMessageable r, int rpcID, short cmd, int messageID, object message, object options = null)
        {
            if (r == null)
            {
                throw new ArgumentNullException();
            }

            //这里是性能敏感区域，使用结构体优化，不使用action闭包
            DealWork work = new DealWork(r, rpcID, cmd, messageID, message, options);
            dealWorkQueue.Enqueue(work);
        }

        /// <summary>
        /// 可能导致大量性能开销
        /// </summary>
        /// <typeparam name="HD"></typeparam>
        /// <param name="header"></param>
        /// <param name="message"></param>
        /// <param name="r"></param>
        [Obsolete("解决不了泛型问题，必须装箱，或生成闭包", true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push<HD>(HD header, object message, IDealMessageable<HD> r)
            where HD : IMessageHeader
        {
            Invoke(() =>
            {
                r?.Deal(header, message);
            });
        }

        /// <summary>
        /// 切换执行线程
        /// <see cref="Switch"/>
        /// </summary>
        /// <param name="action"></param>
        public void Invoke(Action action)
        {
            actions.Enqueue(action);
        }

        /// <summary>
        /// 将一个值或者一组值转换到这个线程,继续执行逻辑.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <remarks>没实现,这个方法存在意义不大</remarks>
        [Obsolete("Use Switch instead", true)]
        public ConfiguredValueTaskAwaitable<T> Push<T>(T value)
        {
            return new ValueTask<T>(value).ConfigureAwait(false);
        }

        /// <summary>
        /// <inheritdoc cref="ThreadSwitcher.Switch"/>
        /// </summary>
        /// <returns></returns>
        [Obsolete("BUG", true)]
        public ConfiguredValueTaskAwaitable Switch()
        {
            return default; // DefaultSwitcher.Switch();
        }

        /// <summary>
        /// 性能比<see cref="Switch"/>更好, 通用性也好,但是没有经过验证有没有bug.
        /// </summary>
        /// <returns></returns>
        public IMiniAwaitable MiniSwitch()
        {
            MiniTask<int> task = MiniTask<int>.Rent();
            MiniTasksSwitcher.Enqueue(task);
            return task;
        }
    }

    public class MessageCtx : SynchronizationContext
    {
        public static readonly MessageCtx Default = new MessageCtx();

        readonly DealWorkQueue dealWorkQueue = new DealWorkQueue();
        public void Update()
        {

        }

        /// <summary>
        /// 专用函数,比<see cref="Switch"/>性能高,但是通用性不好
        /// </summary>
        /// <param name="r"></param>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <param name="options"></param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(IDealMessageable r, int rpcID, short cmd, int messageID, object message, object options = null)
        {
            if (r == null)
            {
                throw new ArgumentNullException();
            }

            //这里是性能敏感区域，使用结构体优化，不使用action闭包
            DealWork work = new DealWork(r, rpcID, cmd, messageID, message, options);
            dealWorkQueue.Enqueue(work);
        }
    }

    /// <summary>
    /// 通用线程切换器,查看meguminexplosion 库
    /// </summary>
    [Obsolete("严重bug,无法实现预定功能. 无法保证先await 后 Tick", true)]
    public partial class ThreadSwitcher
    {
        public static readonly ThreadSwitcher Default = new ThreadSwitcher();

        /// <summary>
        /// 可以合并Source来提高性能,但是会遇到异步后续出现异常的情况,比较麻烦.
        /// 所以每个Switch调用处使用不同的source,安全性更好
        /// </summary>
        readonly ConcurrentQueue<TaskCompletionSource<int>> WaitQueue = new ConcurrentQueue<TaskCompletionSource<int>>();

        /// <summary>
        /// 由指定线程调用,回调其他线程需要切换到这个线程的方法
        /// <para>保证先await 后Tick, 不然 await会发现Task同步完成,无法切换线程.</para>
        /// <para><see cref="Task.Status"/>无法指示是否被await </para>
        /// </summary>
        public void Tick()
        {
            while (WaitQueue.TryDequeue(out var res))
            {
                res.TrySetResult(0);
            }
        }

        /// <summary>
        /// 通用性高,但是用到TaskCompletionSource和异步各种中间对象和异步机制.
        /// 性能开销大不如明确的类型和回调接口.
        /// <para>异步后续在<see cref="Tick"/>线程调用</para>
        /// </summary>
        /// <returns></returns>
        /// <remarks>BUG,无法保证先await 后Tick</remarks>
        public ConfiguredValueTaskAwaitable Switch()
        {
            TaskCompletionSource<int> source = new TaskCompletionSource<int>();
            var a = new ValueTask(source.Task).ConfigureAwait(false);
            WaitQueue.Enqueue(source);
            return a;
        }
    }
}
