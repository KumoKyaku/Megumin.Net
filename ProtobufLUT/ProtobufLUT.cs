using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using System.IO;
using System.Buffers;

namespace Megumin.Message
{
    /// <summary>
    /// 适用于Protobuf协议的查找表           没有测试可能有BUG
    /// </summary>
    public class ProtobufLUT : MessageLUT
    {
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
                var MSGID = type.FirstAttribute<MSGID>();
                if (MSGID != null)
                {
                    Regist(type, MSGID.ID,
                        ProtobufLUTSerializerEx.MakeS(type), ProtobufLUTSerializerEx.MakeD(type), key);
                }
            }
        }

        /// <summary>
        /// 注册消息类型
        /// </summary>
        /// <param name="key"></param>
        public static void Regist<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip)
            where T:IMessage<T>
        {
            var type = typeof(T);
            var MSGID = type.FirstAttribute<MSGID>();
            if (MSGID != null)
            {
                Regist<T>(MSGID.ID,
                    ProtobufLUTSerializerEx.Serialize, ProtobufLUTSerializerEx.MakeD(type), key);
            }
        }
    }

    static class ProtobufLUTSerializerEx
    {
        [ThreadStatic]
        static byte[] cacheBuffer;

        public static byte[] CacheBuffer
        {
            get
            {
                if (cacheBuffer == null)
                {
                    cacheBuffer = new byte[16384];
                }
                return cacheBuffer;
            }
        }


        public static ushort Serialize<T>(T obj, Span<byte> buffer)
            where T:IMessage<T>
        {
            ///等待序列化类库支持Span.
            using (CodedOutputStream co = new CodedOutputStream(CacheBuffer))
            {
                obj.WriteTo(co);
                var lenght = (ushort)co.Position;
                CacheBuffer.AsSpan().Slice(0, lenght).CopyTo(buffer);
                return lenght;
            }
        }

        public static Serialize MakeS2<T>() where T:IMessage<T>
            => MessageLUT.Convert<T>(Serialize);

        public static Serialize MakeS(Type type)
        {
            var methodInfo = typeof(ProtobufLUTSerializerEx).GetMethod(nameof(MakeS2),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);

            return method.Invoke(null,null) as Serialize;
        }

        public static Deserialize Deserialize<T>()
            where T:IMessage<T>
        {
            MessageParser<T> parser = 
                typeof(T).GetProperty("Parser", BindingFlags.Public|BindingFlags.Static)?.GetValue(null) as MessageParser<T>;
            if (parser == null)
            {
                //todo
                throw new Exception();
            }

            return (buffer) =>
                   {
                       using (var stream = new SpanStream(buffer))
                       {
                           IMessage message = parser.ParseFrom(stream);
                           return message;
                       }
                   };
        }

        internal static Deserialize MakeD(Type type)
        {
            dynamic dformatter = type.GetProperty("Parser", BindingFlags.Public|BindingFlags.Static)?.GetValue(null);
            return  (buffer) =>
                    {
                        using (var stream = new SpanStream(buffer))
                        {
                            IMessage message = dformatter.ParseFrom(stream);
                            return message;
                        }
                    };
        }
    }
}
