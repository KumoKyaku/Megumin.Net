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
    }
}
