using Net.Remote;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    ///如果拆离Remote和Socket，就是V1版的模式，缺点在于序列化方法和反序列化方法，
    ///在业务逻辑侧和网络模块侧都有可能需要重写。拆开变成两个并行的继承树，复杂度高，
    ///使用起来并不方便。
    ///现在模式缺点：无法同时从Tcpremote Udpremote继承。如果用户需要在两个协议直接切换，并公用逻辑，
    ///需要用户自己写包装，Onreceive也没有提供回调函数。




    //public class CustomRemote: RpcRemote, IRemote
    //{
    //    public Socket Client { get; }
    //    public bool IsVaild { get; }
    //    public IPEndPoint ConnectIPEndPoint { get; set; }
    //    public EndPoint RemappedEndPoint { get; }
    //    public float LastReceiveTimeFloat { get; }
    //    public int ID { get; }

    //    protected override void Send(int rpcID, object message, object options = null)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    //public interface ISuperSocket
    //{
    //    void Send(int rpcID, object message, object options = null);
    //}

    //public class TcpSuperSocket: ISuperSocket
    //{
    //    public void Send(int rpcID, object message, object options = null)
    //    {
    //        SendWriter.WriteHeader(UdpRemoteMessageDefine.Common);
    //        if (TrySerialize(SendWriter, rpcID, message, options))
    //        {
    //            var (buffer, lenght) = SendWriter.Pop();
    //            SocketSend(buffer, lenght);
    //        }
    //        else
    //        {
    //            var (buffer, lenght) = SendWriter.Pop();
    //            buffer.Dispose();
    //        }
    //    }
    //}
}
