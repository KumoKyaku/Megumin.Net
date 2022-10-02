using Megumin.Message;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
{
    /// <summary>
    /// 泛型报头设计，会导致调度器处性能有问题。
    /// <see cref="MessageThreadTransducer.Push{HD}(HD, object, IDealMessageable{HD})"/>
    /// </summary>
    [Obsolete]
    public interface IMessageHeader
    {
        int RpcID { get; }
        int MessageID { get; }
        short Cmd { get; }
        bool Serialize(IBufferWriter<byte> writer, object options = null);
    }

    [Obsolete]
    public struct MH : IMessageHeader
    {
        public int RpcID { get; set; }
        public int MessageID { get; set; }
        public short Cmd { get; set; } //CMD 为预留，填0

        public bool Serialize(IBufferWriter<byte> writer, object options = null)
        {
            //写入rpcID CMD
            var span = writer.GetSpan(10);
            span.Write(RpcID);
            span.Slice(4).Write(Cmd);
            span.Slice(6).Write(MessageID);
            writer.Advance(10);

            return true;
        }

        public static MH Parse(in ReadOnlySequence<byte> byteSequence)
        {
            //读取RpcID 和 消息ID
            var (RpcID, CMD, MessageID) = byteSequence.ReadHeader();
            MH mH = new MH() { RpcID = 0, MessageID = 0 };
            return default;
        }
    }
}



