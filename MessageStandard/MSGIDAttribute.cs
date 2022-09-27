using System;
using System.Collections.Generic;
using System.Text;

namespace Megumin.Remote
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

        public const int TestPacket1 = -101;
        public const int TestPacket2 = -102;
        /// <summary>
        /// 错误的类型，表示框架未记录的类型。不是void，也不是任何异常ErrorType。
        /// </summary>
        public const int ErrorType = -1;

        /// <summary>
        /// https://github.com/neuecc/MessagePack-CSharp/blob/ffc18319670d49246db1abbd05c404a820280776/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Formatters/PrimitiveObjectFormatter.cs#L16
        /// </summary>
        private static readonly Dictionary<Type, int> TypeToJumpCode = new Dictionary<Type, int>()
        {
            // When adding types whose size exceeds 32-bits, add support in MessagePackSecurity.GetHashCollisionResistantEqualityComparer<T>()
            { typeof(Boolean), 0 },
            { typeof(Char), 1 },
            { typeof(SByte), 2 },
            { typeof(Byte), 3 },
            { typeof(Int16), 4 },
            { typeof(UInt16), 5 },
            { typeof(Int32), 6 },
            { typeof(UInt32), 7 },
            { typeof(Int64), 8 },
            { typeof(UInt64), 9 },
            { typeof(Single), 10 },
            { typeof(Double), 11 },
            { typeof(DateTime), 12 },
            { typeof(string), 13 },
            { typeof(byte[]), 14 },
            { typeof(DateTimeOffset), 15 },
        };

        public const int Boolean = 0;
        public const int Char = 1;
        public const int SByte = 2;
        public const int Byte = 3;
        public const int Int16 = 4;
        public const int UInt16 = 5;
        public const int Int32 = 6;
        public const int UInt32 = 7;
        public const int Int64 = 8;
        public const int UInt64 = 9;
        public const int Single = 10;
        public const int Double = 11;
        public const int DateTime = 12;
        public const int String = 13;
        public const int ByteArray = 14;
        public const int DateTimeOffset = 15;

        /// <summary>
        /// Udp握手连接使用的消息ID编号
        /// </summary>
        public const int UdpConnectMessageID = 101;
        /// <summary>
        /// 心跳包ID，255好识别，buffer[10-13]=[255,0,0,0]
        /// </summary>
        public const int HeartbeatsMessageID = 255;
    }
}
