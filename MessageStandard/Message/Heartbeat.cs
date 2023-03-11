using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Megumin.Message
{
    /// <summary>
    /// 用于RTT时不需要控制线程转换，线程转换带来的延迟是RTT的一部分。
    /// </summary>
    public class Heartbeat : IMeguminFormatter<Heartbeat>
    {
        public static Heartbeat Default { get; } = new Heartbeat();

        internal Heartbeat() { }

        public void Serialize(IBufferWriter<byte> writer, Heartbeat value, object options = null)
        {
            return;
        }

        public int MessageID => MSGID.Heartbeat;
        public Type BindType => typeof(Heartbeat);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            return;
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return Default;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            return Default;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return Default;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, Heartbeat value, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            throw new NotImplementedException();
        }
    }
}
