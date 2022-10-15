using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Net.Remote
{
    /// <summary>
    /// 传输层标准接口
    /// </summary>
    public interface ITransportable : IConnectable, ISocketSendable
    {
        void Send(int rpcID, object message, object options = null);

        /// <summary>
        /// 实际连接的Socket
        /// </summary>
        Socket Client { get; }

        /// <summary>
        /// 当前是否正常工作
        /// </summary>
        bool IsVaild { get; }
    }
}



