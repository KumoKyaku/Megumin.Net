﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using System.IO;
using System.Buffers;
using System.Linq;
using static System.Buffers.ArrayPool<byte>;

namespace Megumin.Remote
{
    /// <summary>
    /// 适用于Protobuf协议的查找表           没有测试可能有BUG
    /// </summary>
    public class ProtobufLUT : MessageLUT
    {
        static ProtobufLUT()
        {
            RegistBasicType();
        }

        /// <summary>
        /// 注册程序集中所有议类
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
            if (type.IsSubclassOf(typeof(IMessage<>)))
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
            where T : IMessage<T>
        {
            var type = typeof(T);
            var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
            if (MSGID != null)
            {
                Regist(new DefaultFormatter<T>(MSGID.ID), key);
            }
        }

        /// <summary>
        /// 注册基础类型
        /// </summary>
        /// <param name="key"></param>
        public static void RegistBasicType(KeyAlreadyHave key = KeyAlreadyHave.Replace)
        {
            //Regist(new DefaultFormatter<string>(MSGID.String), key);
            //Regist(new DefaultFormatter<int>(MSGID.Int32), key);
            //Regist(new DefaultFormatter<long>(MSGID.Int64), key);
            //Regist(new DefaultFormatter<float>(MSGID.Single), key);
            //Regist(new DefaultFormatter<double>(MSGID.Double), key);
            //Regist(new DefaultFormatter<DateTime>(MSGID.DateTime), key);
            //Regist(new DefaultFormatter<DateTimeOffset>(MSGID.DateTimeOffset), key);
            //Regist(new DefaultFormatter<byte[]>(MSGID.ByteArray), key);
        }
    }

    internal class DefaultFormatter<T> : IMeguminFormatter where T : IMessage<T>
    {
        public int MessageID { get; }
        public Type BindType => typeof(T);

        MessageParser<T> parser =
                typeof(T).GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as MessageParser<T>;
        public DefaultFormatter(int messageID)
        {
            MessageID = messageID;
        }

        public void Serialize(IBufferWriter<byte> destination, object value, object options = null)
        {
            IMessage<T> message = (IMessage<T>)value;
            message.WriteTo(destination);
        }

        public void Serialize(Stream destination, object value, object options = null)
        {
            IMessage<T> message = (IMessage<T>)value;
            message.WriteTo(destination);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            var result = parser.ParseFrom(source);
            return result;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            var result = parser.ParseFrom(source);
            return result;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            var result = parser.ParseFrom(source.Span);
            return result;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            var result = parser.ParseFrom(source);
            return result;
        }
    }
}
