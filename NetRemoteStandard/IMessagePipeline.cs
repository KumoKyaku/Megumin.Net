using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Buffers.Binary;
using System.Threading.Tasks;
using Net.Remote;

namespace Megumin.Message
{
    /// <summary>
    /// Tcp打包接口
    /// </summary>
    public interface ITcpPacker
    {
        /// <summary>
        /// 处理粘包，将分好的包放入list中。这里产生一次数据拷贝。
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pushCompleteMessage"></param>
        /// <returns>返回剩余部分</returns>
        ReadOnlySpan<byte> CutOff(ReadOnlySpan<byte> source, IList<IMemoryOwner<byte>> pushCompleteMessage);
    }

    /// <summary>
    /// 消息处理管线
    /// </summary>
    public interface IMessagePipeline:ITcpPacker
    {
        /// <summary>
        /// byte[] -> object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="byteMessage"></param>
        /// <param name="remote"></param>
        void Unpack<T>(IMemoryOwner<byte> byteMessage, T remote)
            where T:ISendMessage,IRemoteID,IUID<int>,IObjectMessageReceiver;
        /// <summary>
        /// object -> byte[]
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        IMemoryOwner<byte> Pack(int rpcID, object message);
        /// <summary>
        /// object -> byte[]
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        IMemoryOwner<byte> Pack(int rpcID, object message, int identifier);
        /// <summary>
        /// object -> byte[]
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="extraMessage"></param>
        /// <returns></returns>
        IMemoryOwner<byte> Pack(int rpcID, object message, ReadOnlySpan<byte> extraMessage);
    }

    /// <summary>
    /// object消息 消费者接口
    /// </summary>
    public interface IObjectMessageReceiver
    {
        /// <summary>
        /// 处理消息实例
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        ValueTask<object> Deal(int rpcID, object message);
    }

    /// <summary>
    /// 串行器接口
    /// </summary>
    public interface IFormater
    {
        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="messageBody"></param>
        /// <returns></returns>
        (int rpcID, object message) Deserialize(int messageID,in ReadOnlyMemory<byte> messageBody);
        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="message"></param>
        /// <param name="rpcID"></param>
        /// <param name="span"></param>
        /// <returns></returns>
        (int messageID, ushort length) Serialize(object message, int rpcID, Span<byte> span);
    }
}
