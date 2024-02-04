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
    /// <see cref="ThreadScheduler.Default"/>
    /// </summary>
    [Obsolete("Use ThreadScheduler.Default instead.")]
    public partial class MessageThreadTransducer
    {
        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        public static void Update(double delta)
        {
            ThreadScheduler.Default.Update(delta);
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
            return ThreadScheduler.Default.Push(rpcID, cmd, messageID, message, r);
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
        internal static void Push(IDealMessageable r, int rpcID, short cmd, int messageID, object message, object options = null)
        {
            ThreadScheduler.Default.Push(r, rpcID, cmd, messageID, options);
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
            ThreadScheduler.Default.Push<HD>(header, message, r);
        }

        /// <summary>
        /// 切换执行线程
        /// <see cref="Switch"/>
        /// </summary>
        /// <param name="action"></param>
        public static void Invoke(Action action)
        {
            ThreadScheduler.Default.Invoke(action);
        }

        /// <summary>
        /// 将一个值或者一组值转换到这个线程,继续执行逻辑.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <remarks>没实现,这个方法存在意义不大</remarks>
        [Obsolete("Use Switch instead", true)]
        public static ConfiguredValueTaskAwaitable<T> Push<T>(T value)
        {
            return ThreadScheduler.Default.Push(value);
        }

        /// <summary>
        /// <inheritdoc cref="ThreadSwitcher.Switch"/>
        /// </summary>
        /// <returns></returns>
        [Obsolete("BUG", true)]
        public static ConfiguredValueTaskAwaitable Switch()
        {
            return ThreadScheduler.Default.Switch();
        }

        /// <summary>
        /// 性能比<see cref="Switch"/>好, 通用性也好,但是没有经过验证有没有bug.
        /// </summary>
        /// <returns></returns>
        public static IMiniAwaitable MiniSwitch()
        {
            return ThreadScheduler.Default.MiniSwitch();
        }
    }
}

