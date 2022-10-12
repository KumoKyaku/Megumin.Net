using Net.Remote;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 线程安全ID生成器
/// </summary>
/// <typeparam name="T"></typeparam>
internal static class InterlockedID<T>
{
    static int id = 0;
    static readonly object locker = new object();
    public static int NewID(int min = 0)
    {
        lock (locker)
        {
            if (id < min)
            {
                id = min;
                return id;
            }

            return id++;
        }
    }
}

namespace Megumin.Remote
{
    /// <summary>
    /// 主动还是被动
    /// </summary>
    public enum ActiveOrPassive
    {
        /// <summary>
        /// 主动的
        /// </summary>
        Active,
        /// <summary>
        /// 被动的
        /// </summary>
        Passive,
    }

    /// <summary>
    /// 记录器
    /// </summary>
    [Obsolete("Use TraceListener instead.", true)]
    public interface IMeguminRemoteLogger
    {
        void Log(string error);
    }

    [Flags]
    public enum Protocol
    {
        Tcp = 1 << 0,
        Udp = 1 << 1,
        Kcp = 1 << 2,
    }

    /// <summary>
    /// 事实上 无论UID是Int,long,还是string,都无法满足全部需求。当你需要其他类型是，请修改源码。
    /// </summary>
    public interface IRemoteUID<T>
    {
        /// <summary>
        /// 预留给用户使用的ID，（用户自己赋值ID，自己管理引用，框架不做处理）
        /// </summary>
        T UID { get; set; }
    }

    public class Utility
    {
        public static void BroadCast(object message, object option, params TcpRemote[] remote)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// <inheritdoc cref="IPipe{T}"/>
    /// <para></para>这是个简单的实现,更复杂使用微软官方实现<see cref="System.Threading.Channels.Channel.CreateBounded{T}(int)"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// <para/> 如果能用 while(true),就不要用递归。
    /// <para/> 在捕捉上下文时，while(true)堆栈更少更清晰，逻辑上复合直觉，不容易爆栈。
    /// <para/> 递归还会导致方法引用计数增加，阅读代码时制造混乱。
    /// </remarks>
    public class QueuePipe<T> : Queue<T>
    {
        readonly object _innerLock = new object();
        private TaskCompletionSource<T> source;

        //线程同步上下文由Task机制保证，无需额外处理
        //SynchronizationContext callbackContext;
        //public bool UseSynchronizationContext { get; set; } = true;

        public virtual void Write(T item)
        {
            lock (_innerLock)
            {
                if (source == null)
                {
                    Enqueue(item);
                }
                else
                {
                    if (Count > 0)
                    {
                        throw new Exception("内部顺序错误，不应该出现，请联系作者");
                    }

                    var next = source;
                    source = null;
                    next.TrySetResult(item);
                }
            }
        }

        public new void Enqueue(T item)
        {
            lock (_innerLock)
            {
                base.Enqueue(item);
            }
        }

        public void Flush()
        {
            lock (_innerLock)
            {
                if (Count > 0)
                {
                    var res = Dequeue();
                    var next = source;
                    source = null;
                    next?.TrySetResult(res);
                }
            }
        }

        public virtual Task<T> ReadAsync()
        {
            lock (_innerLock)
            {
                if (this.Count > 0)
                {
                    var next = Dequeue();
                    return Task.FromResult(next);
                }
                else
                {
                    source = new TaskCompletionSource<T>();
                    return source.Task;
                }
            }
        }

        public ValueTask<T> ReadValueTaskAsync()
        {
            throw new NotImplementedException();
        }
    }
}
