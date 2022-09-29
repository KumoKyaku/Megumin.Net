using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    public class Heartbeat : IMeguminFormater<Heartbeat>
    {
        public static Heartbeat Default { get; } = new Heartbeat();

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
            return new Heartbeat();
        }
    }
}
