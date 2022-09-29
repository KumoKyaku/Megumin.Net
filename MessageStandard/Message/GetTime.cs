using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Message
{
    public partial class GetTime : IMeguminFormater<GetTime>
    {
        public void Serialize(IBufferWriter<byte> writer, GetTime value, object options = null)
        {
            return;
        }

        public int MessageID => MSGID.GetTime;
        public Type BindType => typeof(GetTime);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            return;
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return new GetTime();
        }
    }

    partial class GetTime : IAutoResponseable
    {
        public ValueTask<object> GetResponse(object request)
        {
            return new ValueTask<object>(DateTimeOffset.UtcNow);
        }

        public int PreReceiveType { get; set; } = 2;
    }
}






