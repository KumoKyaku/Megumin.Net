using Megumin.Message;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MessageQueue = System.Collections.Concurrent.ConcurrentQueue<System.Action>;

namespace Megumin.Message
{
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
                res?.Invoke();
            }

            ///                                       双检查（这里使用Count和IsEmpty有不同含义）
            while (actions.TryDequeue(out var callback) || !actions.IsEmpty)
            {
                callback?.Invoke();
            }
        }

        internal static IMiniAwaitable<object> Push(int rpcID, object message, IObjectMessageReceiver r)
        {
            MiniTask<object> task1 = MiniTask<object>.Rent();
            Action action = async () =>
            {
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

                task1.SetResult(response);
            };

            receivePool.Enqueue(action);

            return task1;
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
