//using System;
//using System.Collections.Generic;
//using System.Net.Sockets.Kcp;
//using System.Text;

using System;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    /// <summary>
    /// kcp协议输入输出标准接口
    /// </summary>
    public interface IKcpIO
    {
        /// <summary>
        /// 下层收到数据后添加到kcp协议中
        /// </summary>
        /// <param name="span"></param>
        void Input(ReadOnlySpan<byte> span);
        /// <summary>
        /// 从kcp中取出一个整合完毕的数据包
        /// </summary>
        /// <returns></returns>
        ValueTask<ReadOnlySequence<byte>> Recv();

        /// <summary>
        /// 将要发送到网络的数据Send到kcp协议中
        /// </summary>
        /// <param name="span"></param>
        /// <param name="option"></param>
        void Send(ReadOnlySpan<byte> span, object option = null);
        /// <summary>
        /// 将要发送到网络的数据Send到kcp协议中
        /// </summary>
        /// <param name="span"></param>
        /// <param name="option"></param>
        void Send(ReadOnlySequence<byte> span, object option = null);
        /// <summary>
        /// 从kcp协议中取出需要发送到网络的数据。
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        ValueTask Output(IBufferWriter<byte> writer, object option = null);
    }

    /// <summary>
    /// todo 连接kcpid
    /// </summary>
    public class KcpRemote:UdpRemote
    {
        IKcpIO kcp = null;

        protected internal override void Deal(UdpReceiveResult res)
        {
            kcp.Input(res.Buffer);
        }

        async void KcpRecv()
        {
            while (true)
            {
                var segment = await kcp.Recv();
                ProcessBody(segment);
            }
        }

        protected TestWriter kcpout = new TestWriter(65535);
        async void KcpOutput()
        {
            while (true)
            {
                await kcp.Output(kcpout);
                var (buffer, lenght) = kcpout.Pop();
                if (MemoryMarshal.TryGetArray<byte>(buffer.Memory, out var segment))
                {
                    await UdpClient.SendAsync(segment.Array, lenght, lastRecvIP);
                }
                buffer.Dispose();
            }
        }

        protected override void Send(int rpcID, object message, object options = null)
        {
            if (TrySerialize(testWriter, rpcID, message, options))
            {
                var (buffer, lenght) = testWriter.Pop();
                kcp.Send(buffer.Memory.Span.Slice(0, lenght));
                buffer.Dispose();
            }
            else
            {
                var (buffer, lenght) = testWriter.Pop();
                buffer.Dispose();
            }
        }
    }
}
