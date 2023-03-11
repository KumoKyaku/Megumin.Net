using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MessagePack;
using System.Buffers;
using System.Linq;
using System.IO;

namespace Megumin.Remote
{
    /// <summary>
    /// 适用于MessagePack协议的查找表
    /// </summary>
    public class MessagePackLUT : MessageLUT
    {
        static MessagePackLUT()
        {
            RegistBasicType();
        }

        /// <summary>
        /// 注册程序集中所有协议类
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="key"></param>
        public static void Regist(Assembly assembly, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var types = assembly.GetTypes();
            foreach (var item in types)
            {
                Regist(item, key);
            }
        }

        /// <summary>
        /// 注册消息类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        protected internal static void Regist(Type type, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var attribute = type.GetCustomAttributes<MessagePackObjectAttribute>()?.FirstOrDefault();
            if (attribute != null)
            {
                var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
                if (MSGID != null)
                {
                    var ft = typeof(DefaultFormatter<>);
                    var t = ft.MakeGenericType(new Type[] { type });
                    var instance = Activator.CreateInstance(t, new object[] { MSGID.ID });
                    if (instance is IMeguminFormatter formatter)
                    {
                        Regist(formatter, key);
                    }
                    else
                    {
                        //todo 序列化器构造失败。
                    }
                }
            }
        }

        /// <summary>
        /// 注册消息类型
        /// </summary>
        /// <param name="key"></param>
        public static void Regist<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var type = typeof(T);
            var attribute = type.GetCustomAttributes<MessagePackObjectAttribute>().FirstOrDefault();
            if (attribute != null)
            {
                var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
                if (MSGID != null)
                {
                    Regist(new DefaultFormatter<T>(MSGID.ID), key);
                }
            }
        }

        /// <summary>
        /// 注册基础类型
        /// </summary>
        /// <param name="key"></param>
        public static void RegistBasicType(KeyAlreadyHave key = KeyAlreadyHave.Replace)
        {
            Regist(new DefaultFormatter<string>(MSGID.String), key);
            Regist(new DefaultFormatter<int>(MSGID.Int32), key);
            Regist(new DefaultFormatter<long>(MSGID.Int64), key);
            Regist(new DefaultFormatter<float>(MSGID.Single), key);
            Regist(new DefaultFormatter<double>(MSGID.Double), key);
            Regist(new DefaultFormatter<DateTime>(MSGID.DateTime), key);
            Regist(new DefaultFormatter<DateTimeOffset>(MSGID.DateTimeOffset), key);
            Regist(new DefaultFormatter<byte[]>(MSGID.ByteArray), key);
        }
    }

    internal class DefaultFormatter<T> : IMeguminFormatter
    {
        public int MessageID { get; }
        public Type BindType { get; }

        public DefaultFormatter(int messageID)
        {
            MessageID = messageID;
            BindType = typeof(T);
        }

        public void Serialize(IBufferWriter<byte> destination, object value, object options = null)
        {
            MessagePackSerializer.Serialize<T>(destination, (T)value, options as MessagePackSerializerOptions);
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            MessagePackSerializer.Serialize<T>(destination, (T)value, options as MessagePackSerializerOptions);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            return MessagePackSerializer.Deserialize<T>(source, options as MessagePackSerializerOptions);
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(source.Length))
            {
                source.CopyTo(buffer.Memory.Span);
                return MessagePackSerializer.Deserialize<T>(buffer.Memory, options as MessagePackSerializerOptions);
            }
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            return MessagePackSerializer.Deserialize<T>(source, options as MessagePackSerializerOptions);
        }

        public object Deserialize(in Stream source, object options = null)
        {
            return MessagePackSerializer.Deserialize<T>(source, options as MessagePackSerializerOptions);
        }
    }
}
