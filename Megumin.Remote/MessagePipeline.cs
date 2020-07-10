using Megumin.Message.TestMessage;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Message
{
    /// <summary>
    /// 继承此类时注意使用 sealed 密闭子类以提高效率。
    /// </summary>
    public partial class MessagePipeline:IMessagePipeline
    {
        #region Message

        /// <summary>
        /// 描述消息包长度字节所占的字节数
        /// <para>长度类型ushort，所以一个包理论最大长度不能超过65535字节，框架要求一个包不能大于8192 - 25 个 字节</para>
        /// 
        /// 按照千兆网卡计算，一个玩家每秒10~30包，大约10~30KB，大约能负载3000玩家。
        /// </summary>
        public const int MessageLengthByteCount = sizeof(ushort);

        /// <summary>
        /// 消息包类型ID 字节长度
        /// </summary>
        public const int MessageIDByteCount = sizeof(int);

        /// <summary>
        /// 消息包类型ID 字节长度
        /// </summary>
        public const int RpcIDByteCount = sizeof(ushort);

        /// <summary>
        /// 报头初始偏移6, rpcID贴合在消息正文，不算报头。
        /// </summary>
        public const int HeaderOffset = 2 + 4;

        #endregion

        /// <summary>
        /// 默认开启线程转换
        /// </summary>
        public bool Post2ThreadScheduler { get; set; } = true;
        public readonly static MessagePipeline Default = new MessagePipeline();



        /// <summary>
        /// 分离粘包
        /// <para> <see cref="Pack(int, object, ReadOnlySpan{byte})"/> 对应 </para>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pushCompleteMessage"></param>
        /// <returns>剩余的半包。</returns>
        public virtual ReadOnlySpan<byte> CutOff(ReadOnlySpan<byte> source, IList<IMemoryOwner<byte>> pushCompleteMessage)
        {
            var length = source.Length;
            ///已经完整读取消息包的长度
            int offset = 0;
            ///长度至少要大于2（2个字节表示消息总长度）
            while (length - offset > 2)
            {

                ///取得单个消息总长度
                ushort size = source.Slice(offset).ReadUshort();
                if (length - offset < size)
                {
                    ///剩余消息长度不是一个完整包
                    break;
                }

                /// 使用内存池
                var newMsg = BufferPool.Rent(size);

                source.Slice(offset, size).CopyTo(newMsg.Memory.Span);
                pushCompleteMessage.Add(newMsg);

                offset += size;
            }

            ///返回剩余的半包。
            return source.Slice(offset, length - offset);
        }

        public virtual async void Unpack<T>(IMemoryOwner<byte> packet, T bufferReceiver)
            where T : ISendMessage, IRemoteID, IUID<int>, IObjectMessageReceiver
        {
            try
            {
                var memory = packet.Memory;

                var (messageID, extraMessage, messageBody) = UnPack(memory);

                if (await PreDeserialize(messageID, extraMessage, messageBody, bufferReceiver))
                {
                    var (rpcID, message) = Deserialize(messageID, messageBody);

                    if (await PostDeserialize(messageID, extraMessage, rpcID, message, bufferReceiver))
                    {
                        var post2 = CheckPost2ThreadScheduler(messageID, message);
                        if (post2)
                        {
                            var resp = await MessageThreadTransducer.Push(rpcID, message, bufferReceiver);

                            Reply(bufferReceiver, extraMessage, rpcID, resp);
                        }
                        else
                        {
                            var resp = await bufferReceiver.Deal(rpcID, message);

                            if (resp is Task<object> task)
                            {
                                resp = await task;
                            }

                            if (resp is ValueTask<object> vtask)
                            {
                                resp = await vtask;
                            }

                            Reply(bufferReceiver, extraMessage, rpcID, resp);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                UnityEngine.Debug.Log($"Unpack -- {e};");
                throw;
            }
            finally
            {
                packet.Dispose();
            }
        }

        /// <summary>
        /// 精确控制各个消息是否切换到主线程。
        /// <para>用于处理在某些时钟精确的且线程无关消息时跳过轮询等待。</para>
        /// 例如：同步两个远端时间戳的消息。
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual bool CheckPost2ThreadScheduler(int messageID, object message)
        {
            return Post2ThreadScheduler;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bufferReceiver"></param>
        /// <param name="extraMessage"></param>
        /// <param name="rpcID"></param>
        /// <param name="resp"></param>
        protected virtual void Reply<T>(T bufferReceiver, ReadOnlyMemory<byte> extraMessage, int rpcID, object resp)
            where T : ISendMessage, IRemoteID, IUID<int>, IObjectMessageReceiver
        {
            if (resp != null)
            {
                RoutingInformationModifier routeTableWriter = new RoutingInformationModifier(extraMessage);
                routeTableWriter.ReverseDirection();
                var b = Pack(rpcID * -1, resp, routeTableWriter);
                bufferReceiver.SendAsync(b);
                routeTableWriter.Dispose();
            }
        }

        /// <summary>
        /// 转发
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="remote"></param>
        /// <param name="messageID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="messageBody"></param>
        /// <param name="forwarder"></param>
        public virtual void Forward<T>(T bufferReceiver, int messageID, ReadOnlyMemory<byte> extraMessage, ReadOnlyMemory<byte> messageBody, IForwarder forwarder) 
            where T : IRemoteID,IUID<int>
        {
            RoutingInformationModifier modifier = extraMessage;
            if (modifier.Mode == RouteMode.Null)
            {
                modifier.Identifier = bufferReceiver.UID;
            }
            else if (modifier.Mode == RouteMode.Find)
            {
                modifier = new RoutingInformationModifier(extraMessage);
                modifier.AddNode(bufferReceiver, forwarder);
            }
            else
            {
                modifier = new RoutingInformationModifier(extraMessage);
                modifier.MoveCursorNext();
            }

            forwarder.SendAsync(Pack(messageID, extraMessage: modifier, messageBody.Span));
            modifier.Dispose();
        }
    }

    ///处理路由转发过程
    partial class MessagePipeline
    {
        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageID"></param>
        /// <param name="routeTable"></param>
        /// <param name="messageBody"></param>
        /// <param name="bufferReceiver"></param>
        /// <returns></returns>
        public virtual ValueTask<bool> PreDeserialize<T>(int messageID,ReadOnlyMemory<byte> extraMessage,
            ReadOnlyMemory<byte> messageBody,T bufferReceiver)
            where T:ISendMessage,IRemoteID,IUID<int>,IObjectMessageReceiver
        {
            return new ValueTask<bool>(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="rpcID"></param>
        /// <param name="routeTable"></param>
        /// <param name="message"></param>
        /// <param name="bufferReceiver"></param>
        /// <returns></returns>
        public virtual ValueTask<bool> PostDeserialize<T>(int messageID,ReadOnlyMemory<byte> extraMessage,
            int rpcID, object message,T bufferReceiver)
            where T:ISendMessage,IRemoteID,IUID<int>,IObjectMessageReceiver
        {
            return new ValueTask<bool>(true);
        }
    }

    ///打包封包
    partial class MessagePipeline
    {
        /// <summary>
        /// 普通打包
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public virtual IMemoryOwner<byte> Pack(int rpcID, object message)
        {
            ///序列化用buffer,使用内存池
            using (var memoryOwner = BufferPool.Rent(16384))
            {
                Span<byte> span = memoryOwner.Memory.Span;

                var (messageID, length) = Serialize(message, rpcID, span);

                ///             这里进行拷贝并得到新的发送用buffer             此处省略了额外消息  
                var sendbuffer = Pack(messageID, extraMessage:RoutingInformationModifier.Empty, span.Slice(0, length));
                return sendbuffer;
            }
            ///释放 序列化用buffer
        }

        /// <summary>
        /// 转发打包
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public virtual IMemoryOwner<byte> Pack(int rpcID, object message, int identifier)
        {
            ///序列化用buffer,使用内存池
            using (var memoryOwner = BufferPool.Rent(16384))
            {
                Span<byte> span = memoryOwner.Memory.Span;

                var (messageID, length) = Serialize(message, rpcID, span);

                var routeTable = new RoutingInformationModifier(identifier);
                var res = Pack(messageID, extraMessage:routeTable, span.Slice(0, length));
                routeTable.Dispose();
                return res;
            }
        }

        /// <summary>
        /// 返回消息打包
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="extraMessage"></param>
        /// <returns></returns>
        public virtual IMemoryOwner<byte> Pack(int rpcID, object message, ReadOnlySpan<byte> extraMessage)
        {
            ///序列化用buffer,使用内存池
            using (var memoryOwner = BufferPool.Rent(16384))
            {
                Span<byte> span = memoryOwner.Memory.Span;

                var (messageID, length) = Serialize(message, rpcID, span);

                return Pack(messageID, extraMessage, span.Slice(0, length));
            }
        }

        /// <summary>
        /// 封装将要发送的字节消息,这个方法控制消息字节的布局
        /// <para>框架使用的字节布局 2总长度 + 4消息ID +  extraMessage + messageBody</para>
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="messageBody"></param>
        /// <returns>框架使用BigEndian</returns>
        public virtual IMemoryOwner<byte> Pack(int messageID, ReadOnlySpan<byte> extraMessage, ReadOnlySpan<byte> messageBody)
        {
            if (extraMessage.IsEmpty)
            {
                throw new ArgumentNullException($"The extra message part is at least 1 in length/额外消息部分至少长度为1");
            }
            ushort totolLength = (ushort)(HeaderOffset + extraMessage.Length + messageBody.Length);

            ///申请发送用 buffer ((框架约定1)发送字节数组发送完成后由发送逻辑回收)         额外信息的最大长度17
            var sendbufferOwner = BufferPool.Rent(totolLength);
            var span = sendbufferOwner.Memory.Span;

            ///写入报头 大端字节序写入
            totolLength.WriteTo(span);
            messageID.WriteTo(span.Slice(2));


            ///拷贝额外消息
            extraMessage.CopyTo(span.Slice(HeaderOffset));
            ///拷贝消息正文
            messageBody.CopyTo(span.Slice(HeaderOffset + extraMessage.Length));

            return sendbufferOwner;
        }

        /// <summary>
        /// 解析报头 (长度至少要大于6（6个字节也就是一个报头长度）)
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度小于报头长度</exception>
        public virtual (ushort totalLenght, int messageID)
            ParsePacketHeader(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= HeaderOffset)
            {
                ushort size = buffer.ReadUshort();

                int messageID = buffer.Slice(2).ReadInt();

                return (size, messageID);
            }
            else
            {
                throw new ArgumentOutOfRangeException("The data length is less than the header length/数据长度小于报头长度");
            }
        }

        /// <summary>
        /// 解包。 这个方法解析消息字节的布局
        /// <para> 和 <see cref="Pack(int, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> 对应</para>
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <remarks>分离消息是使用报头描述的长度而不能依赖于Span长度</remarks>
        public virtual (int messageID, ReadOnlyMemory<byte> extraMessage, ReadOnlyMemory<byte> messageBody)
            UnPack(ReadOnlyMemory<byte> buffer)
        {
            ReadOnlySpan<byte> span = buffer.Span;
            var (totalLenght, messageID) = ParsePacketHeader(span);
            var extralength = span.Slice(HeaderOffset)[0];

            var extraMessage = buffer.Slice(HeaderOffset, extralength);

            int start = HeaderOffset + extralength;
            ///分离消息是使用报头描述的长度而不能依赖于Span长度
            var messageBody = buffer.Slice(start, totalLenght - start);
            return (messageID, extraMessage, messageBody);
        }
    }

    ///消息正文处理
    partial class MessagePipeline//:IFormater
    {
        /// <summary>
        /// 序列化消息阶段
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="span"></param>
        /// <param name="message"></param>
        /// <param name="rpcID"></param>
        /// <returns></returns>
        public virtual (int messageID, ushort length)
            Serialize(object message, int rpcID, Span<byte> span)
        {
            ///rpcID直接附加值消息正文前4位。
            rpcID.WriteTo(span);
            var (messageID, length) = MessageLUT.Serialize(message, span.Slice(4));
            return (messageID, (ushort)(length + 4));
        }

        /// <summary>
        /// 反序列化消息阶段
        /// </summary>
        /// <returns></returns>
        public virtual (int rpcID,object message) Deserialize(int messageID,in ReadOnlyMemory<byte> messageBody)
        {
            var rpcID = messageBody.Span.ReadInt();
            var message = MessageLUT.Deserialize(messageID, messageBody.Slice(4));
            return (rpcID, message);
        }
    }

    internal class GateServerMessagePipeline:MessagePipeline
    {
        ///这是如何使用转发的例子
        public override ValueTask<bool> PreDeserialize<T>(int messageID,ReadOnlyMemory<byte> extraMessage,ReadOnlyMemory<byte> messageBody, T bufferReceiver)
        {
            RoutingInformationModifier information = extraMessage;
            if (information.Mode == RouteMode.Backward || information.Mode == RouteMode.Forward)
            {
                var forwarder = GetForward(information.Next);
                if (forwarder != null)
                {
                    Forward(bufferReceiver, messageID, extraMessage, messageBody, forwarder);
                    return new ValueTask<bool>(false);
                }
            }
            return new ValueTask<bool>(true);
        }

        private IForwarder GetForward(int? next)
        {
            throw new NotImplementedException();
        }

        private object GetNewReceiver(int identifier)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask<bool> PostDeserialize<T>(int messageID, ReadOnlyMemory<byte> extraMessage, int rpcID, object message, T bufferReceiver)
        {
            RoutingInformationModifier information = extraMessage;
            ///当转发到路由表末尾时，寻找消息接收者，可能时Remote本身，也可能能是其他任意符合接口的对象。
            if (information.Identifier == bufferReceiver.ID)
            {
                return true;
            }
            else
            {
                ///指定新的消息接收者
                bufferReceiver = (T)GetNewReceiver(information.Identifier);
                if (bufferReceiver != null)
                {
                    if (Post2ThreadScheduler)
                    {
                        var resp = await MessageThreadTransducer.Push(rpcID, message, bufferReceiver);

                        Reply(bufferReceiver, extraMessage, rpcID, resp);
                    }
                    else
                    {
                        var resp = await bufferReceiver.Deal(rpcID, message);

                        if (resp is Task<object> task)
                        {
                            resp = await task;
                        }

                        if (resp is ValueTask<object> vtask)
                        {
                            resp = await vtask;
                        }

                        Reply(bufferReceiver, extraMessage, rpcID, resp);
                    }
                    return false;
                }
                return true;
            }


        }
    }

    internal class BattleServerMP:MessagePipeline
    {
        
    }



    public class TestFunction
    {
        static int totalCount = 0;
        public static async ValueTask<object> DealMessage(object message,IReceiveMessage receiver)
        {
            totalCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    if (totalCount % 100 == 0)
                    {
                        Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}------总消息数{totalCount}"); 
                    }
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return new TestPacket2 { Value = packet2.Value };
                default:
                    break;
            }
            return null;
        }
    }


    class RPipev2
    {
        
        /// <summary>
        /// 处理消息
        /// </summary>
        public static IMiniAwaitable<object> Deal(int RpcID,int MessageID,object message)
        {
            return default;
        }
    }
}


/// <summary>
/// 小端
/// </summary>
internal static class SpanByteEX_45F6E953
{
    public static int ReadInt(this in ReadOnlySequence<byte> byteSequence)
    {
        unsafe
        {
            Span<byte> span = stackalloc byte[4];
            byteSequence.CopyTo(span);
            return span.ReadInt();
        }
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="num"></param>
    /// <param name="span"></param>
    /// <returns>offset</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTo(this int num, Span<byte> span)
    {
        BinaryPrimitives.WriteInt32LittleEndian(span, num);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTo(this ushort num, Span<byte> span)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, num);
        return 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTo(this short num, Span<byte> span)
    {
        BinaryPrimitives.WriteInt16LittleEndian(span, num);
        return 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTo(this long num, Span<byte> span)
    {
        BinaryPrimitives.WriteInt64LittleEndian(span, num);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt(this ReadOnlySpan<byte> span)
        => BinaryPrimitives.ReadInt32LittleEndian(span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUshort(this ReadOnlySpan<byte> span)
        => BinaryPrimitives.ReadUInt16LittleEndian(span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadShort(this ReadOnlySpan<byte> span)
        => BinaryPrimitives.ReadInt16LittleEndian(span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadLong(this ReadOnlySpan<byte> span)
        => BinaryPrimitives.ReadInt64LittleEndian(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt(this Span<byte> span)
        => BinaryPrimitives.ReadInt32LittleEndian(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUshort(this Span<byte> span)
        => BinaryPrimitives.ReadUInt16LittleEndian(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadShort(this Span<byte> span)
        => BinaryPrimitives.ReadInt16LittleEndian(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadLong(this Span<byte> span)
        => BinaryPrimitives.ReadInt64LittleEndian(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt(this Memory<byte> span)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUshort(this Memory<byte> span)
        => BinaryPrimitives.ReadUInt16LittleEndian(span.Span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadShort(this Memory<byte> span)
        => BinaryPrimitives.ReadInt16LittleEndian(span.Span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadLong(this Memory<byte> span)
        => BinaryPrimitives.ReadInt64LittleEndian(span.Span);
}