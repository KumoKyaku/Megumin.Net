using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            var str = MessageLUT.Deserialize<string>(source, options);
            return new Authentication() { Token = str };
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            var str = MessageLUT.Deserialize<string>(source, options);
            return new Authentication() { Token = str };
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            var str = MessageLUT.Deserialize<string>(source, options);
            return new Authentication() { Token = str };
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }
    }
}
