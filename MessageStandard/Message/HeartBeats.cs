using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    public class HeartBeats : IMeguminFormater<HeartBeats>
    {
        public void Serialize(IBufferWriter<byte> writer, HeartBeats value, object options = null)
        {
            return;
        }

        public int MessageID => MSGID.Heartbeats;
        public Type BindType => typeof(HeartBeats);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            return;
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return new HeartBeats();
        }
    }
}
