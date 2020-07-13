using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MessagePack;
using System.Buffers;
using System.Linq;

namespace Megumin.Remote
{
    /// <summary>
    /// 适用于MessagePack协议的查找表
    /// </summary>
    public class MessagePackLUT : MessageLUT
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
        protected internal static void Regist(Type type, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var attribute = type.GetCustomAttributes<MessagePackObjectAttribute>()?.FirstOrDefault();
            if (attribute != null)
            {
                var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
                if (MSGID != null)
                {
                    var ft = typeof(MPFormater<>);
                    var t = ft.MakeGenericType(new Type[] { type });
                    var instance = Activator.CreateInstance(t, new object[] { MSGID.ID });
                    if (instance is IMeguminFormater formater)
                    {
                        Regist(formater, key);
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
                    Regist(new MPFormater<T>(MSGID.ID), key);
                }
            }
        }
    }

    internal class MPFormater<T> : IMeguminFormater
    {
        public int MessageID { get; }
        public Type BindType { get; }

        public MPFormater(int messageID)
        {
            MessageID = messageID;
            BindType = typeof(T);
        }

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            MessagePackSerializer.Serialize<T>(writer, (T)value, options as MessagePackSerializerOptions);
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            return MessagePackSerializer.Deserialize<T>(byteSequence, options as MessagePackSerializerOptions);
        }
    }
}
