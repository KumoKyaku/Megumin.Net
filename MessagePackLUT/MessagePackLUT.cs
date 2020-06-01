using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MessagePack;
using System.Buffers;
using System.Linq;

namespace Megumin.Message
{
    /// <summary>
    /// 适用于MessagePack协议的查找表
    /// </summary>
    public class MessagePackLUT: MessageLUT
    {
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
        protected internal static void Regist(Type type,KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var attribute = type.GetCustomAttributes<MessagePackObjectAttribute>()?.FirstOrDefault();
            if (attribute != null)
            {
                var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
                if (MSGID != null)
                {
                    Regist(type, MSGID.ID,
                        MessagePackSerializerEx.MakeS(type), MessagePackSerializerEx.MakeD(type), key);
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
                    Regist<T>(MSGID.ID,
                        MessagePackSerializerEx.Serialize, MessagePackSerializerEx.MakeD(type), key);
                }
            }
        }
    }

    static class MessagePackSerializerEx
    {
        public static ushort Serialize<T>(T obj, Span<byte> buffer)
        {
            var sbuffer = MessagePackSerializer.Serialize(obj);
            sbuffer.AsSpan().CopyTo(buffer);
            return (ushort)sbuffer.Length;
        }

        public static Serialize MaskS2<T>() => MessageLUT.Convert<T>(Serialize);

        public static Serialize MakeS(Type type)
        {
            var methodInfo = typeof(MessagePackSerializerEx).GetMethod(nameof(MaskS2),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);

            return method.Invoke(null,null) as Serialize;
        }

        public static T Deserilizer<T>(ReadOnlyMemory<byte> buffer)
        {
            using (var stream = new SpanStream(buffer))
            {
                return MessagePackSerializer.Deserialize<T>(stream);
            }
        }

        public static Deserialize MakeD(Type type)
        {
            var methodInfo = typeof(MessagePackSerializerEx).GetMethod(nameof(Deserilizer),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);

            return method.CreateDelegate(typeof(Deserialize)) as Deserialize;
        }
    }
}
