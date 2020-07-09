using Megumin.Message;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MessageQueue = System.Collections.Concurrent.ConcurrentQueue<Megumin.Message.WorkRequest>;

namespace Megumin.Message
{
    internal struct WorkRequest
    {
        readonly int rpcID;
        readonly MiniTask<object> task;
        readonly object message;
        readonly IObjectMessageReceiver r;

        internal WorkRequest(MiniTask<object> task, int rpcID, object message, IObjectMessageReceiver r)
        {
            this.rpcID = rpcID;
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
            var response = await r.Deal(rpcID, message);

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
    /// <summary>
    /// 接收消息池
    /// </summary>
    public partial class MessageThreadTransducer
    {
        static MessageQueue receivePool = new MessageQueue();

        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        public static void Update(double delta)
        {
            while (receivePool.TryDequeue(out var res))
            {
                res.Invoke();
            }

            while (actions.TryDequeue(out var callback))
            {
                callback?.Invoke();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IMiniAwaitable<object> Push(int rpcID, object message, IObjectMessageReceiver r)
        {
            //这里是性能敏感区域，使用结构体优化，不使用action闭包
            MiniTask<object> task = MiniTask<object>.Rent();
            WorkRequest work = new WorkRequest(task, rpcID, message, r);
            receivePool.Enqueue(work);
            return task;
        }

        static readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();

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
