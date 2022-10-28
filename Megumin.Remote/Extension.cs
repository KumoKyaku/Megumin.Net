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


    static readonly RpcRemote BroadCastHelper = new RpcRemote();

    /// <summary>
    /// Tcp广播。
    /// 默认序列化。所有Remote只序列化一次。如果重写了序列化规则或者报头就不能用这个方法了。
    /// 具体用例还要具体优化。交给用户自行处理。
    /// </summary>
    /// <typeparam name="R"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="remotes"></param>
    /// <param name="message"></param>
    /// <param name="options"></param>
    public static void BroadCastTcp<R, T>(this R remotes, T message, object options = null)
        where R : IEnumerable<IRemote>
    {
        var writer = new TcpBufferWriter();

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
        else
        {
            writer.Discard();
        }
        //这里就不处理缓冲区归还到内存池了，太麻烦。涉及到生命周期。
    }

    public static void BroadCastUdp<R, T>(this R remotes, T message, object options = null)
        where R : IEnumerable<IRemote>
    {
        var writer = new UdpBufferWriter(0x10000);
        writer.WriteHeader(UdpRemoteMessageDefine.UdpData);
        if (BroadCastHelper.TrySerialize(writer, 0, message, options))
        {
            foreach (var item in remotes)
            {
                if (item.Transport is UdpTransport udpTransport)
                {
                    ///不要用<see cref="UdpTransport.SocketSend(ISendBlock)"/>,不应该调用 sendBlock.SendSuccess();
                    udpTransport.SocketSend(writer.SendSegment);
                }
            }
        }
        else
        {
            writer.Discard();
        }
        //这里就不处理缓冲区归还到内存池了，太麻烦。涉及到生命周期。
    }

    public static void BroadCastKcp<R, T>(this R remotes, T message, object options = null)
        where R : IEnumerable<IRemote>
    {
        var writer = new UdpBufferWriter(0x10000);
        if (BroadCastHelper.TrySerialize(writer, 0, message, options))
        {
            foreach (var item in remotes)
            {
                if (item.Transport is KcpTransport kcpTransport)
                {
                    kcpTransport.KcpCore.Send(writer.SendMemory.Span);
                }
            }
            writer.SendSuccess();
        }
        else
        {
            writer.Discard();
        }
    }
}


