using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
        void Deal(int rpcID, short cmd, int messageID, object message);
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
        readonly int rpcID;
        readonly short cmd;
        readonly int messageID;
        readonly object message;
        readonly IDealMessageable r;

        internal DealWork(int rpcID, short cmd, int messageID,
            object message, IDealMessageable r)
        {
            this.rpcID = rpcID;
            this.cmd = cmd;
            this.messageID = messageID;
            this.message = message;
            this.r = r;
        }

        public void Invoke()
        {
            r.Deal(rpcID, cmd, messageID, message);
        }
    }
    /// <summary>
    /// 接收消息池
    /// </summary>
    public partial class MessageThreadTransducer
    {
        static readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();
        [Obsolete("设计缺陷,线程转换不应该带有异步逻辑,严重增加复杂性", true)]
        static readonly RequestWorkQueue requestWorkQueue = new RequestWorkQueue();
        static readonly DealWorkQueue dealWorkQueue = new DealWorkQueue();
        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        public static void Update(double delta)
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
        internal static IMiniAwaitable<object> Push(int rpcID, short cmd, int messageID, object message, IObjectMessageReceiver r)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Push(int rpcID, short cmd, int messageID, object message, IDealMessageable r)
        {
            if (r == null)
            {
                throw new ArgumentNullException();
            }

            //这里是性能敏感区域，使用结构体优化，不使用action闭包
            DealWork work = new DealWork(rpcID, cmd, messageID, message, r);
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
        internal static void Push<HD>(HD header, object message, IDealMessageable<HD> r)
            where HD : IMessageHeader
        {
            Invoke(() =>
            {
                r?.Deal(header, message);
            });
        }

        /// <summary>
        /// 切换执行线程
        /// </summary>
        /// <param name="action"></param>
        public static void Invoke(Action action)
        {
            actions.Enqueue(action);
        }
    }
}
