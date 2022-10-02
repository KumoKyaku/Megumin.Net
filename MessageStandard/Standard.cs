using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Message
{
    /// <summary>
    /// 由发送端和消息协议控制的的响应机制
    /// </summary>
    public interface IPreReceiveable
    {
        /// <summary>
        /// 1: Echo
        /// 2: AutoResp
        /// </summary>
        /// <remarks>具体实现在PreReceive函数中</remarks>
        int PreReceiveType { get; }
    }

    /// <summary>
    /// <inheritdoc/>
    /// 自动回复
    /// </summary>
    public interface IAutoResponseable : IPreReceiveable
    {
        ValueTask<object> GetResponse(object request);
    }

    /// <summary>
    /// 指定接受端处理此消息时是否使用线程调度。
    /// </summary>
    /// <remarks>
    /// 目前仅用于GetTime。在校准两个进程的TimeStamp时，不希望线程切换功能产生不必要的延迟。
    /// </remarks>
    public interface IReceiveThreadControlable
    {
        bool? ReceiveThreadPost2ThreadScheduler { get; }
    }
}
