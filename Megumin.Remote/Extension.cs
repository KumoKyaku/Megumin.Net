using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Megumin.Remote;
using Net.Remote;
using static Megumin.Remote.TcpSendPipe;

public static class RemoteExtension_1D96E84960F84A7DBFCE21028A82F32A
{
    /// <summary>
    /// 广播。
    /// 没有优化，每个Remote都会序列化一次
    /// </summary>
    /// <typeparam name="R"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="remotes"></param>
    /// <param name="message"></param>
    /// <param name="options"></param>
    public static void BroadCast<R, T>(this R remotes, T message, object options = null)
        where R : IEnumerable<IRemote>
    {
        foreach (var item in remotes)
        {
            item?.Send(message, options);
        }
    }


    static RpcRemote BroadCastHelper = new RpcRemote();
    static TcpSendPipe TcpBroadCastPipe = new TcpSendPipe();

    /// <summary>
    /// 广播。
    /// 默认序列化。所有Remote只序列化一次。
    /// </summary>
    /// <typeparam name="R"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="remotes"></param>
    /// <param name="message"></param>
    /// <param name="options"></param>
    public static void BroadCastTcpUnsafe<R, T>(this R remotes, T message, object options = null)
        where R : IEnumerable<IRemote>
    {
        var writer = TcpBroadCastPipe.GetWriter();

        if (BroadCastHelper.TrySerialize(writer, 0, message, options))
        {
            writer.WriteLengthOnHeader();
            foreach (var item in remotes)
            {
                if (item.Transport is TcpTransport tcpTransport)
                {
                    tcpTransport.SendPipe.Push2Queue(writer);
                }
            }
        }
    }
}


