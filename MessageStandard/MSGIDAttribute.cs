using System;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Message
{
    /// <summary>
    /// 使用MessageID来为每一个消息指定一个唯一ID(-999~999 被框架占用)。
    /// 请查看常量。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Interface|AttributeTargets.Struct|AttributeTargets.Enum)]
    public sealed class MSGID : Attribute
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attribute"></param>
        public static implicit operator int(MSGID attribute) => attribute.ID;

        /// <summary>
        /// 消息ID
        /// </summary>
        /// <param name="id"></param>
        public MSGID(int id)
        {
            this.ID = id;
        }

        /// <summary>
        /// 消息类唯一编号
        /// </summary>
        public int ID { get; }

        //public static implicit operator MSGIDAttribute(int id)
        //{
        //    return new MSGIDAttribute(id);
        //}

        public const int TestPacket1ID = -101;
        public const int TestPacket2ID = -102;
        /// <summary>
        /// 错误的类型，表示框架未记录的类型。不是void，也不是任何异常ErrorType。
        /// </summary>
        public const int ErrorType = -1;
        public const int StringID = 11;
        public const int IntID = 12;
        public const int FloatID = 13;
        public const int LongID = 14;
        public const int DoubleID = 15;
        /// <summary>
        /// Udp握手连接使用的消息ID编号
        /// </summary>
        public const int UdpConnectMessageID = 101;
        /// <summary>
        /// 心跳包ID，255好识别，buffer[2-5]=[255,0,0,0]
        /// </summary>
        public const int HeartbeatsMessageID = 255;
    }
}
