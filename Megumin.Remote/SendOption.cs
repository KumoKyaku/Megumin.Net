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
        bool? RpcComplatePost2ThreadScheduler { get; }
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
        public static readonly SendOption Never = new SendOption() { MillisecondsDelay = -1, MillisecondsTimeout = -1 };
        public static readonly SendOption Echo = new SendOption() { MillisecondsDelay = 30000, MillisecondsTimeout = 30000, Cmd = 1 };
        public int MillisecondsDelay { get; set; } = 30000;
        public int MillisecondsTimeout { get; set; } = 30000;
        public short Cmd { get; set; } = 0;
        public bool? RpcComplatePost2ThreadScheduler { get; set; } = null;
        public bool ForceUdp { get; set; } = false;

    }
}




