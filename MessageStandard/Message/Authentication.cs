using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    public class Authentication : IMeguminFormater<Authentication>
    {
        public string Token { get; set; }

        public void Serialize(IBufferWriter<byte> writer, Authentication value, object options = null)
        {
            MessageLUT.Serialize(writer, value.Token, options);
        }

        public int MessageID => MSGID.Authentication;
        public Type BindType => typeof(Authentication);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (Authentication)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            var str = MessageLUT.Deserialize<string>(byteSequence, options);
            return new Authentication() { Token = str };
        }
    }
}
