using System;
using System.Collections.Generic;
using Megumin.Message;

namespace Megumin.Remote
{
    public interface IFormatterContainer
    {
        Dictionary<int, IMeguminFormatter> IDDic { get; }
        Dictionary<Type, IMeguminFormatter> TypeDic { get; }

        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <param name="meguminFormatter"></param>
        /// <param name="key"></param>
        void Regist(IMeguminFormatter meguminFormatter, KeyAlreadyHave key = KeyAlreadyHave.Skip);

        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        void RegistIMeguminFormatter<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip)
            where T : class, IMeguminFormatter, new();
    }

    public class FormatterContainerBase
    {
        public Dictionary<int, IMeguminFormatter> IDDic { get; } = new();
        public Dictionary<Type, IMeguminFormatter> TypeDic { get; } = new();

        /// <summary>
        /// 注册序列化器
        /// </summary>
        /// <param name="meguminFormatter"></param>
        /// <param name="key"></param>
        public void Regist(IMeguminFormatter meguminFormatter, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (meguminFormatter.BindType == null)
            {
                throw new ArgumentException("序列化器没有绑定类型");
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:

                    if (IDDic.TryGetValue(meguminFormatter.MessageID, out var old))
                    {
                        IDDic.Remove(old.MessageID);
                        TypeDic.Remove(old.BindType);
                    }

                    if (TypeDic.TryGetValue(meguminFormatter.BindType, out var old2))
                    {
                        IDDic.Remove(old2.MessageID);
                        TypeDic.Remove(old2.BindType);
                    }
                    IDDic[meguminFormatter.MessageID] = meguminFormatter;
                    TypeDic[meguminFormatter.BindType] = meguminFormatter;

                    break;
                case KeyAlreadyHave.Skip:
                    if (IDDic.ContainsKey(meguminFormatter.MessageID)
                         || TypeDic.ContainsKey(meguminFormatter.BindType))
                    {
                        return;
                    }

                    IDDic[meguminFormatter.MessageID] = meguminFormatter;
                    TypeDic[meguminFormatter.BindType] = meguminFormatter;
                    break;
                case KeyAlreadyHave.ThrowException:
                    if (IDDic.ContainsKey(meguminFormatter.MessageID))
                    {
                        throw new ArgumentException
                            ($"消息ID冲突，同一个ID再次注册。 当前ID:{meguminFormatter.MessageID}。 当前类型:{meguminFormatter.BindType.FullName}。" +
                            $"已有类型：{IDDic[meguminFormatter.MessageID].BindType.FullName}");
                    }

                    if (TypeDic.ContainsKey(meguminFormatter.BindType))
                    {
                        throw new ArgumentException
                            ($"消息类型冲突，同一个类型再次注册。当前类型:{meguminFormatter.BindType.FullName}。 当前ID:{meguminFormatter.MessageID}。" +
                            $"已有ID：{TypeDic[meguminFormatter.BindType].MessageID}。");
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
        public void RegistIMeguminFormatter<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip)
            where T : class, IMeguminFormatter, new()
        {
            T f = new();
            Regist(f, key);
        }
    }

    public class MeguminFormatterContainer : FormatterContainerBase, IFormatterContainer
    {
        public MeguminFormatterContainer()
        {
            //注册基础类型
            Regist(new StringFormatter());
            Regist(new IntFormatter());
            Regist(new FloatFormatter());
            Regist(new LongFormatter());
            Regist(new DoubleFormatter());
            Regist(new DatetimeFormatter());
            Regist(new DatetimeOffsetFormatter());
            Regist(new ByteArrayFormatter());

            //注册内置消息
            Regist(new TestPacket1());
            Regist(new TestPacket2());
            Regist(new TestPacket3());
            Regist(new TestPacket4());
            Regist(new Heartbeat());
            Regist(new GetTime());
            Regist(new Authentication());
        }
    }
}



