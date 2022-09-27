using Megumin.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    /// <summary>
    /// 内置UTF8 string 格式化器，性能低没有优化
    /// </summary>
    internal class StringFormatter : IMeguminFormater<string>
    {
        internal static readonly Encoding UTF8 = new UTF8Encoding(false);
        public void Serialize(IBufferWriter<byte> writer, string value, object options = null)
        {
            var bytes = UTF8.GetBytes(value);
            writer.Write(bytes);
        }

        public int MessageID => MSGID.StringID;
        public Type BindType => typeof(string);

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serialize(writer, value.ToString(), options);
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return UTF8.GetString(byteSequence.ToArray());
        }
    }
}
