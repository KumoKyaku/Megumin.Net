using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Message
{
    public partial class GetTime : IMeguminFormater<GetTime>
    {
        public void Serialize(IBufferWriter<byte> writer, GetTime value, object options = null)
        {
            var span = writer.GetSpan(4);
            span.Write(PreReceiveType);
            writer.Advance(4);

            var post = writer.GetSpan(1);
            post.Write(ReceiveThreadPost2ThreadScheduler);
            writer.Advance(1);
        }

        public int MessageID => MSGID.GetTime;
        public Type BindType => typeof(GetTime);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, (GetTime)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            var result = new GetTime();
            result.PreReceiveType = source.ReadInt();
            result.ReceiveThreadPost2ThreadScheduler = source.ReadBoolNullable(4);
            return result;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            var result = new GetTime();
            result.PreReceiveType = source.ReadInt();
            result.ReceiveThreadPost2ThreadScheduler = source.Slice(4).ReadBoolNullable();
            return result;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            var result = new GetTime();
            result.PreReceiveType = source.Span.ReadInt();
            result.ReceiveThreadPost2ThreadScheduler = source.Span.Slice(4).ReadBoolNullable();
            return result;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, GetTime value, object options = null)
        {
            throw new NotImplementedException();
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            throw new NotImplementedException();
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

    partial class GetTime : IReceiveThreadControlable
    {
        public bool? ReceiveThreadPost2ThreadScheduler { get; set; } = false;
    }
}






