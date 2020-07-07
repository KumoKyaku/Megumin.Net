using Megumin.Message.TestMessage;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
    public interface IFormater
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
    /// 将消息从0位置开始 序列化 到 指定buffer中,返回序列化长度
    /// </summary>
    /// <param name="message">消息实例</param>
    /// <param name="buffer">给定的buffer,长度为16384</param>
    /// <returns>序列化消息的长度</returns>
    public delegate ushort RegistSerialize<in T>(T message, Span<byte> buffer);

    /// <summary>
    /// 值类型使用这个委托注册，相比值类型使用RegistSerialize注册，可以省一点点性能，但是仍然不建议用值类型消息。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="message"></param>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public delegate ushort ValueRegistSerialize<T>(in T message, Span<byte> buffer);

    public delegate ushort Serialize(object message, Span<byte> buffer);

    /// <summary>
    /// 消息查找表
    /// <seealso cref="Message.Serialize"/>  <seealso cref="Message.Deserialize"/>
    /// </summary>
    public class MessageLUT
    {
        static MessageLUT()
        {
            ///注册测试消息和内置消息
            Regist<TestPacket1>(MSGID.TestPacket1ID, TestPacket1.S, TestPacket1.D);
            Regist<TestPacket2>(MSGID.TestPacket2ID, TestPacket2.S, TestPacket2.D);
            ///5个基础类型
            Regist<string>(MSGID.StringID, BaseType.Serialize, BaseType.StringDeserialize);
            Regist<int>(MSGID.IntID, BaseType.Serialize,BaseType.IntDeserialize);
            Regist<long>(MSGID.LongID, BaseType.Serialize,BaseType.LongDeserialize);
            Regist<float>(MSGID.FloatID, BaseType.Serialize,BaseType.FloatDeserialize);
            Regist<double>(MSGID.DoubleID, BaseType.Serialize,BaseType.DoubleDeserialize);


            ///框架用类型
            Regist<HeartBeatsMessage>(MSGID.HeartbeatsMessageID,
                HeartBeatsMessage.Seiralizer, HeartBeatsMessage.Deserilizer, KeyAlreadyHave.ThrowException);


            Regist<UdpConnectMessage>(MSGID.UdpConnectMessageID,
                UdpConnectMessage.Serialize, UdpConnectMessage.Deserialize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Serialize Convert<T>(RegistSerialize<T> registSerialize)
        {
            return (obj, buffer) =>
            {
                if (obj is T message)
                {
                    return registSerialize(message, buffer);
                }
                throw new InvalidCastException(typeof(T).FullName);
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Serialize Convert<T>(ValueRegistSerialize<T> registSerialize)
        {
            return (obj, buffer) =>
            {
                if (obj is T message)
                {
                    return registSerialize(in message, buffer);
                }
                throw new InvalidCastException(typeof(T).FullName);
            };
        }

        static readonly Dictionary<int, (Type type,Deserialize deserialize)> dFormatter = new Dictionary<int, (Type type,Deserialize deserialize)>();
        static readonly Dictionary<Type, (int MessageID, Serialize serialize)> sFormatter = new Dictionary<Type, (int MessageID, Serialize serialize)>();
        
        protected static void AddSFormatter(Type type, int messageID, Serialize seiralize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (type == null || seiralize == null)
            {
                throw new ArgumentNullException();
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    sFormatter[type] = (messageID, seiralize);
                    return;
                case KeyAlreadyHave.Skip:
                    if (sFormatter.ContainsKey(type))
                    {
                        return;
                    }
                    else
                    {
                        sFormatter.Add(type, (messageID, seiralize));
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    sFormatter.Add(type, (messageID, seiralize));
                    break;
            }
        }

        protected static void AddDFormatter(int messageID,Type type, Deserialize deserilize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (deserilize == null)
            {
                throw new ArgumentNullException();
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    dFormatter[messageID] = (type, deserilize);
                    return;
                case KeyAlreadyHave.Skip:
                    if (dFormatter.ContainsKey(messageID))
                    {
                        DebugLogger.LogWarning($"[{type.FullName}]和[{dFormatter[messageID].type.FullName}]的消息ID[{messageID}]冲突。");
                        return;
                    }
                    else
                    {
                        dFormatter.Add(messageID, (type, deserilize));
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    dFormatter.Add(messageID, (type, deserilize));
                    break;
            }
        }

        public static void Regist(Type type, int messageID, Serialize seiralize, Deserialize deserilize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(type, messageID, seiralize, key);
            AddDFormatter(messageID,type, deserilize, key);
        }

        public static void Regist<T>(int messageID, RegistSerialize<T> seiralize, Deserialize deserilize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(typeof(T), messageID, Convert(seiralize), key);
            AddDFormatter(messageID,typeof(T), deserilize, key);
        }

        public static void Regist<T>(int messageID, ValueRegistSerialize<T> seiralize, Deserialize deserilize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(typeof(T), messageID, Convert(seiralize), key);
            AddDFormatter(messageID, typeof(T), deserilize, key);
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer16384"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"> 消息长度大于8192 - 25(框架用长度),请拆分发送。"</exception>
        /// <remarks>框架中TCP接收最大支持8192，所以发送也不能大于8192，为了安全起见，框架提供的字节数组长度是16384的。</remarks>
        public static (int messageID, ushort length)
            Serialize(object message,Span<byte> buffer16384)
        {
            var type = message.GetType();
            if (sFormatter.TryGetValue(type, out var sf))
            {
                ///序列化消息
                var (MessageID, Seiralize) = sf;

                if (Seiralize == null)
                {
                    DebugLogger.LogError($"消息[{type.Name}]的序列化函数没有找到。");
                    return (-1, default);
                }

                ushort length = Seiralize(message, buffer16384);

                //if (length > 8192 - 25)
                //{
                //    //BufferPool.Push16384(buffer16384);
                //    ///消息过长
                //    throw new ArgumentOutOfRangeException(
                //        $"The message length is greater than {8192 - 25}," +
                //        $" Please split to send./" +
                //        $"消息长度大于{8192 - 25}," +
                //        $"请拆分发送。");
                //}

                return (MessageID, length);
            }
            else
            {
                DebugLogger.LogError($"消息[{type.Name}]的序列化函数没有找到。");
                return (-1, default);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Deserialize(int messageID,in ReadOnlyMemory<byte> body)
        {
            if (dFormatter.ContainsKey(messageID))
            {
                try
                {
                    return dFormatter[messageID].deserialize(body);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

            }
            else
            {
                Debug.LogError($"消息ID为[{messageID}]的反序列化函数没有找到。");
                return null;
            }
            return null;
        }

        /// <summary>
        /// 查找消息类型
        /// </summary>
        /// <param name="messageID"></param>
        /// <returns></returns>
        public static Type GetType(int messageID)
        {
            if (dFormatter.TryGetValue(messageID,out var res))
            {
                return res.type;
            }
            else
            {
                return null;
            }
        }

        public static bool TryGetType(int messageID,out Type type)
        {
            if (dFormatter.TryGetValue(messageID, out var res))
            {
                type = res.type;
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
        public static int? GetID<T>()
        {
            if (sFormatter.TryGetValue(typeof(T),out var res))
            {
                return res.MessageID;
            }
            return null;
        }

        public static bool TryGetID<T>(out int ID)
        {
            if (sFormatter.TryGetValue(typeof(T), out var res))
            {
                ID = res.MessageID;
                return true;
            }

            ID = -1;
            return false;
        }
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
