using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
{
    /// <summary>
    /// 心跳消息
    /// </summary>
    /// <remarks> 
    ///     <code>
    ///         MessageLUT.Regist(Heartbeat.Default);
    ///     </code> 
    /// </remarks>
    public class Heartbeat : IMeguminFormater
    {
        public static Heartbeat Default { get; } = new Heartbeat();
        public int MessageID { get; } = MSGID.HeartbeatsMessageID;
        public Type BindType { get; } = typeof(Heartbeat);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {

        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return Default;
        }
    }
}
