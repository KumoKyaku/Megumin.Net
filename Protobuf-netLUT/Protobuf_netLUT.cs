using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using ProtoBuf;
using static System.Buffers.ArrayPool<byte>;

namespace Megumin.Remote
{
    /// <summary>
    /// 适用于Protobuf-net协议的查找表    没有测试
    /// </summary>
    public class Protobuf_netLUT : MessageLUT
    {
        static Protobuf_netLUT()
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
            var attribute = type.GetCustomAttributes<ProtoContractAttribute>().FirstOrDefault();
            if (attribute != null)
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
        {
            var type = typeof(T);
            var attribute = type.GetCustomAttributes<ProtoContractAttribute>().FirstOrDefault();
            if (attribute != null)
            {
                var MSGID = type.GetCustomAttributes<MSGID>().FirstOrDefault();
                if (MSGID != null)
                {
                    Regist(new DefaultFormater<T>(MSGID.ID), key);
                }
            }
        }

        /// <summary>
        /// 注册基础类型
        /// </summary>
        /// <param name="key"></param>
        public static void RegistBasicType(KeyAlreadyHave key = KeyAlreadyHave.Replace)
        {
            Regist(new DefaultFormater<string>(MSGID.String), key);
            Regist(new DefaultFormater<int>(MSGID.Int32), key);
            Regist(new DefaultFormater<long>(MSGID.Int64), key);
            Regist(new DefaultFormater<float>(MSGID.Single), key);
            Regist(new DefaultFormater<double>(MSGID.Double), key);
            Regist(new DefaultFormater<DateTime>(MSGID.DateTime), key);
            Regist(new DefaultFormater<DateTimeOffset>(MSGID.DateTimeOffset), key);
            Regist(new DefaultFormater<byte[]>(MSGID.ByteArray), key);
        }
    }

    internal class DefaultFormater<T> : IMeguminFormater
    {
        public int MessageID { get; }
        public Type BindType { get; }

        public DefaultFormater(int messageID)
        {
            MessageID = messageID;
            BindType = typeof(T);
        }

        public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            Serializer.Serialize<T>(writer, (T)value, options);
        }

        public object Deserialize(in ReadOnlySequence<byte> source, object options = null)
        {
            var result = Serializer.Deserialize<T>(source, userState: options);
            return result;
        }

        public object Deserialize(in ReadOnlySpan<byte> source, object options = null)
        {
            var result = Serializer.Deserialize<T>(source, userState: options);
            return result;
        }

        public object Deserialize(in ReadOnlyMemory<byte> source, object options = null)
        {
            var result = Serializer.Deserialize<T>(source, userState: options);
            return result;
        }

        public object Deserialize(in Stream source, object options = null)
        {
            var result = Serializer.Deserialize<T>(source, userState: options);
            return result;
        }
    }

    //internal class PBnetFormaterOld<T> : IMeguminFormater
    //{
    //    public int MessageID { get; }
    //    public Type BindType { get; }

    //    private BufferWriterBytesSteam bufferSteam;
    //    private MemoryStream dmemoryS;

    //    public PBnetFormaterOld(int messageID)
    //    {
    //        MessageID = messageID;
    //        BindType = typeof(T);
    //        bufferSteam = new BufferWriterBytesSteam();
    //        dmemoryS = new MemoryStream();
    //    }

    //    public void Serialize(IBufferWriter<byte> writer, object value, object options = null)
    //    {
    //        bufferSteam.BufferWriter = writer;
    //        Serializer.Serialize<T>(bufferSteam, (T)value);
    //        bufferSteam.BufferWriter = null;
    //    }

    //    public object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null)
    //    {
    //        int length = (int)byteSequence.Length;
    //        dmemoryS.Seek(0, SeekOrigin.Begin);
    //        dmemoryS.SetLength(length);
    //        byteSequence.CopyTo(dmemoryS.GetBuffer());
    //        var result = Serializer.Deserialize<T>(dmemoryS);
    //        return result;
    //    }
    //}
}
