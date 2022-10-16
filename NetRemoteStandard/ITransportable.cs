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
        void Send<T>(T message, int rpcID, object options = null);

        /// <summary>
        /// 实际连接的Socket
        /// </summary>
        Socket Client { get; }

        /// <summary>
        /// 当前是否正常工作
        /// </summary>
        bool IsVaild { get; }

        /// <summary>
        /// 断线重连
        /// </summary>
        /// <param name="transportable"></param>
        /// <returns></returns>
        bool ReConnectFrom(ITransportable transportable);
    }
}



