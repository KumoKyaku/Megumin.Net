using System;
using Net.Remote;

namespace Megumin.Remote
{
    //线程控制不是NetRemoteStandard的一部分，所以相关接口放在实现这里

    /// <summary>
    /// 由SendOption针对消息实例设置RpcSend异步后续的执行，是否使用MessageThreadTransducer
    /// </summary>
    public interface IRpcThreadOption
    {
        /// <summary>
        /// <para/> true: 强制使用ThreadScheduler;
        /// <para/> false: 强制不使用ThreadScheduler;
        /// <para/> null表示不控制，由其他设置决定;
        /// </summary>
        [Obsolete("use RpcComplatePost2ThreadSchedulerType instead.", true)]
        bool? RpcComplatePost2ThreadScheduler { get; }

        /// <summary>
        /// <para/>  1: 强制使用ThreadScheduler;
        /// <para/>  0: 表示不控制，由其他设置决定;
        /// <para/> -1: 强制不使用ThreadScheduler;
        /// </summary>
        int RpcComplatePost2ThreadSchedulerType { get; }
    }

    public interface IForceUdpDataOnKcpRemote
    {
        /// <summary>
        /// rpc时只能自己这面能UDP，对面返回时还是Kcp
        /// </summary>
        bool ForceUdp { get; }
    }

    public class SendOption : IRpcTimeoutOption, ICmdOption, IRpcThreadOption, IForceUdpDataOnKcpRemote
    {
        public static readonly SendOption Never = new SendOption() { MillisecondsTimeout = -1 };
        public static readonly SendOption Echo = new SendOption() { MillisecondsTimeout = 30000, Cmd = 1 };
        public int MillisecondsTimeout { get; set; } = 30000;
        public short Cmd { get; set; } = 0;
        [Obsolete("use RpcComplatePost2ThreadSchedulerType instead.")]
        public bool? RpcComplatePost2ThreadScheduler { get; set; } = null;
        public int RpcComplatePost2ThreadSchedulerType { get; set; } = 0;
        public bool ForceUdp { get; set; } = false;

    }
}




