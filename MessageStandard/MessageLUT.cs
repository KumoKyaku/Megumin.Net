using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace Megumin.Message
{
    /// <summary>
    /// Key冲突改怎么做
    /// </summary>
    public enum KeyAlreadyHave
    {
        /// <summary>
        /// 替换
        /// </summary>
        Replace,
        /// <summary>
        /// 跳过
        /// </summary>
        Skip,
        /// <summary>
        /// 抛出异常
        /// </summary>
        ThrowException,
    }

    /// <summary>
    /// 通用序列化库接口
    /// </summary>
    public interface IMeguminFormater
    {
        /// <summary>
        /// 消息识别ID
        /// </summary>
        int MessageID { get; }
        /// <summary>
        /// 消息类型
        /// </summary>
        Type BindType { get; }
        /// <summary>
        /// 序列化函数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <remarks>序列化函数不在提供序列化多少字节，需要在writer中自己统计</remarks>
        void Serialize(IBufferWriter<byte> writer, object value, object options = null);
        /// <summary>
        /// 反序列化函数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="byteSequence"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        object Deserialize(in ReadOnlySequence<byte> byteSequence, object options = null);
    }

    /// <summary>
    /// 消息查找表
    /// <seealso cref="Message.Serialize"/>  <seealso cref="Message.Deserialize"/>
    /// </summary>
    public class MessageLUT
    {
        static readonly Dictionary<int, IMeguminFormater> IDDic = new Dictionary<int, IMeguminFormater>();
        static readonly Dictionary<Type, IMeguminFormater> TypeDic = new Dictionary<Type, IMeguminFormater>();
        
        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <param name="meguminFormater"></param>
        /// <param name="key"></param>
        public static void Regist(IMeguminFormater meguminFormater, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (meguminFormater.BindType == null)
            {
                throw new ArgumentException("序列化器没有绑定类型");
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:

                    if (IDDic.TryGetValue(meguminFormater.MessageID,out var old))
                    {
                        IDDic.Remove(old.MessageID);
                        TypeDic.Remove(old.BindType);
                    } 

                    if (TypeDic.TryGetValue(meguminFormater.BindType,out var old2))
                    {
                        IDDic.Remove(old2.MessageID);
                        TypeDic.Remove(old2.BindType);
                    }
                    IDDic[meguminFormater.MessageID] = meguminFormater;
                    TypeDic[meguminFormater.BindType] = meguminFormater;

                    break;
                case KeyAlreadyHave.Skip:
                    if (IDDic.ContainsKey(meguminFormater.MessageID)
                         || TypeDic.ContainsKey(meguminFormater.BindType))
                    {
                        return;
                    }

                    IDDic[meguminFormater.MessageID] = meguminFormater;
                    TypeDic[meguminFormater.BindType] = meguminFormater;
                    break;
                case KeyAlreadyHave.ThrowException:
                    if (IDDic.ContainsKey(meguminFormater.MessageID))
                    {
                        throw new ArgumentException
                            ($"消息ID冲突，同一个ID再次注册。 当前ID:{meguminFormater.MessageID}。 当前类型:{meguminFormater.BindType.FullName}。" +
                            $"已有类型：{IDDic[meguminFormater.MessageID].BindType.FullName}");
                    }

                    if (TypeDic.ContainsKey(meguminFormater.BindType))
                    {
                        throw new ArgumentException
                            ($"消息类型冲突，同一个类型再次注册。当前类型:{meguminFormater.BindType.FullName}。 当前ID:{meguminFormater.MessageID}。" +
                            $"已有ID：{TypeDic[meguminFormater.BindType].MessageID}。");
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        public static void Regist<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip) where T : class, IMeguminFormater, new()
        {
            T f = new T();
            Regist(f, key);
        }

        /// <summary>
        /// 序列化一个对象到指定writer
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <returns>消息ID</returns>
        /// <remarks>序列化函数不在提供序列化多少字节，需要在writer中自己统计</remarks>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static int Serialize(IBufferWriter<byte> writer, object value, object options = null)
        {
            var type = value.GetType();
            var formater = TypeDic[type];
            formater.Serialize(writer, value, options);
            return formater.MessageID;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="byteSequence"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static object Deserialize(int messageID, in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            var formater = IDDic[messageID];
            var result = formater.Deserialize(byteSequence, options);
            return result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="byteSequence"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>有时即使类型不匹配也能反序列化成功，但得到的值时错误的</remarks>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public static T Deserialize<T>(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            var type = typeof(T);
            var formater = TypeDic[type];
            var result = formater.Deserialize(byteSequence, options);
            return (T)result;
        }

        /// <summary>
        /// 查找消息类型
        /// </summary>
        /// <param name="messageID"></param>
        /// <returns></returns>
        public static Type GetType(int messageID)
        {
            return IDDic[messageID].BindType;
        }

        /// <summary>
        /// 查找消息类型
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool TryGetType(int messageID,out Type type)
        {
            if (IDDic.TryGetValue(messageID, out var res))
            {
                type = res.BindType;
                return true;
            }
            else
            {
                type = null;
                return false;
            }
        }

        /// <summary>
        /// 查找消息ID
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetID<T>()
        {
            var type = typeof(T);
            var formater = TypeDic[type];
            return formater.MessageID;
        }

        /// <summary>
        /// 查找消息ID
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static int GetID(Type type)
        {
            var formater = TypeDic[type];
            return formater.MessageID;
        }

        /// <summary>
        /// 查找消息ID
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ID"></param>
        /// <returns></returns>
        public static bool TryGetID<T>(out int ID)
        {
            if (TypeDic.TryGetValue(typeof(T), out var res))
            {
                ID = res.MessageID;
                return true;
            }

            ID = -1;
            return false;
        }

        /// <summary>
        /// 查找消息ID
        /// </summary>
        /// <param name="type"></param>
        /// <param name="ID"></param>
        /// <returns></returns>
        public static bool TryGetID(Type type, out int ID)
        {
            if (TypeDic.TryGetValue(type, out var res))
            {
                ID = res.MessageID;
                return true;
            }

            ID = -1;
            return false;
        }
    }

    /// <summary>
    /// 包装<see cref="IBufferWriter{T}"/><see cref="byte"/>成一个长度无限的只写流，
    /// 只有<see cref="Write(byte[], int, int)"/>函数起作用。
    /// </summary>
    public class BufferWriterBytesSteam : Stream
    {
        public IBufferWriter<byte> BufferWriter { get; set; }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var destination = BufferWriter.GetSpan(count);
            var span = new Span<byte>(buffer, offset, count);
            span.CopyTo(destination);
            BufferWriter.Advance(count);
        }

        public override bool CanRead { get; } = false;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = true;
        public override long Length { get; } = long.MaxValue;
        public override long Position { get; set; }
    }



    public interface ILogger
    {
        void Log(object message);
        void LogError(object message);
        void LogWarning(object message);
    }

    /// <summary>
    /// 
    /// </summary>
    public static class DebugLogger
    {
        public static ILogger Logger { get; set; }
        public static void Log(object message)
            => Logger?.Log(message);

        public static void LogError(object message)
            => Logger?.LogError(message);

        public static void LogWarning(object message)
            => Logger?.LogWarning(message);
    }
}
