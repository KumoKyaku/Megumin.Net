using System;
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
                    var ft = typeof(DefaultFormater<>);
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
            where T:IMessage<T>
        {
            var type = typeof(T);
            var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
            if (MSGID != null)
            {
                Regist(new DefaultFormater<T>(MSGID.ID), key);
            }
        }

        /// <summary>
        /// 注册基础类型
        /// </summary>
        /// <param name="key"></param>
        public static void RegistBasicType(KeyAlreadyHave key = KeyAlreadyHave.Replace)
        {
            //Regist(new DefaultFormater<string>(MSGID.StringID), key);
            //Regist(new DefaultFormater<int>(MSGID.IntID), key);
            //Regist(new DefaultFormater<long>(MSGID.LongID), key);
            //Regist(new DefaultFormater<float>(MSGID.FloatID), key);
            //Regist(new DefaultFormater<double>(MSGID.DoubleID), key);
        }
    }

    internal class DefaultFormater<T> : IMeguminFormater where T : IMessage<T>
    {
        public int MessageID { get; }
        public Type BindType { get; }

        private BufferWriterBytesSteam bufferSteam;
        private CodedOutputStream codeOutputSteam;

        MessageParser<T> parser =
                typeof(T).GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as MessageParser<T>;
        public DefaultFormater(int messageID)
        {
            MessageID = messageID;
            BindType = typeof(T);
            bufferSteam = new BufferWriterBytesSteam();
            codeOutputSteam = new CodedOutputStream(bufferSteam);
        }

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            bufferSteam.BufferWriter = writer;
            IMessage<T> message = (IMessage<T>)value;
            message.WriteTo(codeOutputSteam);
            codeOutputSteam.Flush();
            bufferSteam.BufferWriter = null;
        }

        public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            int length = (int)byteSequence.Length;
            var buffer = Shared.Rent(length);
            byteSequence.CopyTo(buffer.AsSpan());
            var result = parser.ParseFrom(buffer,0, length);
            return result;
        }
    }

    
}
