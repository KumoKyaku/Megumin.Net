﻿using Megumin.Message;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Megumin.Remote
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

    public interface IMeguminSerializer<T, V>
    {
        /// <summary>
        /// 序列化函数
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <remarks>序列化函数不在提供序列化多少字节，需要在destination中自己统计</remarks>
        void Serialize(T destination, V value, object options = null);
    }

    public interface IMeguminDeserializer<T>
    {
        /// <summary>
        /// 反序列化函数
        /// </summary>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>返回值不考虑泛型，泛型虽然能避免值类型消息装箱，但是调用时要使用反射去转化为
        /// 对应类型接口，在rpc回调转型处仍然会有类型匹配失败问题，得不偿失。</remarks>
        object Deserialize(in T source, object options = null);
    }

    /// <summary>
    /// 通用序列化库接口
    /// </summary>
    /// <remarks>
    /// 用户自己实现时可以不必实现所有函数，不同的协议用的是不同的函数，可以有选择的实现即可。
    /// </remarks>
    public interface IMeguminFormatter :
        IMeguminSerializer<IBufferWriter<byte>, object>,
        IMeguminSerializer<Stream, object>,
        IMeguminDeserializer<Stream>,
        IMeguminDeserializer<ReadOnlySequence<byte>>,
        IMeguminDeserializer<ReadOnlyMemory<byte>>
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
        /// 反序列化函数
        /// </summary>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>返回值不考虑泛型，泛型虽然能避免值类型消息装箱，但是调用时要使用反射去转化为
        /// 对应类型接口，在rpc回调转型处仍然会有类型匹配失败问题，得不偿失。</remarks>
        object Deserialize(in ReadOnlySpan<byte> source, object options = null);
    }

    /// <summary>
    /// 不要使用协变，会导致序列化错误
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMeguminFormatter<T> : IMeguminFormatter
    {
        /// <summary>
        /// 序列化函数
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <remarks>序列化函数不在提供序列化多少字节，需要在writer中自己统计</remarks>
        void Serialize(IBufferWriter<byte> destination, T value, object options = null);

        /// <summary>
        /// 序列化函数
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <remarks>序列化函数不在提供序列化多少字节，需要在writer中自己统计</remarks>
        void Serialize(Stream destination, T value, object options = null);
    }

    /// <summary>
    /// 对象自身就是序列化器，是MessageLut没注册时的fallback。
    /// </summary>
    [Obsolete("没有MessageLut根本就找不到类型，这个思路不成立。", true)]
    public interface IMeguminSelfFormatter : IMeguminFormatter
    {
        /// <summary>
        /// 先构造对象，然后自己解析。
        /// </summary>
        /// <param name="byteSequence"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        bool SelfDeserialize(in ReadOnlySequence<byte> byteSequence, object options = null);
    }

    /// <summary>
    /// 消息查找表
    /// </summary>
    public partial class MessageLUT
    {
        /// <summary>
        /// Formatter 容器。
        /// 允许用户设置自定义Formatter
        /// </summary>
        public static IFormatterContainer FormatterContainer { get; set; } = new MeguminFormatterContainer();


        public static Dictionary<int, IMeguminFormatter> IDDic
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return FormatterContainer.IDDic;
            }
        }

        public static Dictionary<Type, IMeguminFormatter> TypeDic
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return FormatterContainer.TypeDic;
            }
        }

        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <param name="meguminFormatter"></param>
        /// <param name="key"></param>
        public static void Regist(IMeguminFormatter meguminFormatter, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            FormatterContainer.Regist(meguminFormatter, key);
        }

        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        public static void RegistIMeguminFormatter<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip)
            where T : class, IMeguminFormatter, new()
        {
            FormatterContainer.RegistIMeguminFormatter<T>(key);
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
        public static bool TryGetType(int messageID, out Type type)
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
            var formatter = TypeDic[type];
            return formatter.MessageID;
        }

        /// <summary>
        /// 查找消息ID
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static int GetID(Type type)
        {
            var formatter = TypeDic[type];
            return formatter.MessageID;
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

        public static bool TryGetFormatter(Type type, out IMeguminFormatter formatter)
        {
            return TypeDic.TryGetValue(type, out formatter);
        }

        public static bool TryGetFormatter(int messageID, out IMeguminFormatter formatter)
        {
            return IDDic.TryGetValue(messageID, out formatter);
        }
    }

    public partial class MessageLUT
    {
        ///// <summary>
        ///// 序列化一个对象到指定writer
        ///// </summary>
        ///// <param name="writer"></param>
        ///// <param name="value"></param>
        ///// <param name="options"></param>
        ///// <returns>消息ID</returns>
        ///// <remarks>序列化函数不在提供序列化多少字节，需要在writer中自己统计</remarks>
        ///// <exception cref="KeyNotFoundException"></exception>
        ///// <exception cref="ArgumentNullException"></exception>
        //public static int Serialize(IBufferWriter<byte> writer, object value, object options = null)
        //{
        //    var type = value.GetType();
        //    var formatter = TypeDic[type];
        //    formatter.Serialize(writer, value, options);
        //    return formatter.MessageID;
        //}

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
        public static int Serialize<T>(IBufferWriter<byte> writer, T value, object options = null)
        {
            //这里一定要从value获取真实类型，防止类型隐式转型导致类型推导不正确，所以不能用typeof(T)。
            //使用泛型的目的时尽可能的减少装箱。
            //var testtype = typeof(T);
            var type = value.GetType();

            var formatter = TypeDic[type];
            if (formatter is IMeguminFormatter<T> gformatter)
            {
                gformatter.Serialize(writer, value, options);
            }
            else
            {
                formatter.Serialize(writer, value, options);
            }

            return formatter.MessageID;
        }
    }

    public partial class MessageLUT
    {
        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static object Deserialize(int messageID, in ReadOnlySequence<byte> source, object options = null)
        {
            var formatter = IDDic[messageID];
            var result = formatter.Deserialize(source, options);
            return result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static object Deserialize(int messageID, in ReadOnlySpan<byte> source, object options = null)
        {
            var formatter = IDDic[messageID];
            var result = formatter.Deserialize(source, options);
            return result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static object Deserialize(int messageID, in ReadOnlyMemory<byte> source, object options = null)
        {
            var formatter = IDDic[messageID];
            var result = formatter.Deserialize(source, options);
            return result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static object Deserialize(int messageID, in Stream source, object options = null)
        {
            var formatter = IDDic[messageID];
            var result = formatter.Deserialize(source, options);
            return result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>有时即使类型不匹配也能反序列化成功，但得到的值时错误的</remarks>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public static T Deserialize<T>(in ReadOnlySequence<byte> source, object options = null)
        {
            var type = typeof(T);
            var formatter = TypeDic[type];
            var result = formatter.Deserialize(source, options);
            return (T)result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>有时即使类型不匹配也能反序列化成功，但得到的值时错误的</remarks>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public static T Deserialize<T>(in ReadOnlySpan<byte> source, object options = null)
        {
            var type = typeof(T);
            var formatter = TypeDic[type];
            var result = formatter.Deserialize(source, options);
            return (T)result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>有时即使类型不匹配也能反序列化成功，但得到的值时错误的</remarks>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public static T Deserialize<T>(in ReadOnlyMemory<byte> source, object options = null)
        {
            var type = typeof(T);
            var formatter = TypeDic[type];
            var result = formatter.Deserialize(source, options);
            return (T)result;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="source"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <remarks>有时即使类型不匹配也能反序列化成功，但得到的值时错误的</remarks>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public static T Deserialize<T>(in Stream source, object options = null)
        {
            var type = typeof(T);
            var formatter = TypeDic[type];
            var result = formatter.Deserialize(source, options);
            return (T)result;
        }
    }

    public partial class MessageLUT
    {
        public static T TestType<T>(T original)
        {
            MessageLUTTestBuffer wr = new MessageLUTTestBuffer();
            MessageLUT.Serialize(wr, original);
            return MessageLUT.Deserialize<T>(wr.ReadOnlySpan);
        }
    }
}
