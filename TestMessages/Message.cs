using System;
using MessagePack;
using Megumin.Remote;
using ProtoBuf;

namespace Megumin.Remote.Test
{

    [MSGID(1000)]
    [ProtoContract]
    [MessagePackObject]
    public class Message
    {
    }

    [MSGID(1001)]
    [ProtoContract]
    [MessagePackObject]
    public class Login
    {
        [ProtoMember(1)]
        [Key(0)]
        public string IP { get; set; }
    }

    [MSGID(1002)]
    [ProtoContract]
    [MessagePackObject]
    public class LoginResult
    {
        [ProtoMember(1)]
        [Key(0)]
        public string TempKey { get; set; }
    }

    [MSGID(1003)]
    [ProtoContract]
    [MessagePackObject]
    public class Login2Gate
    {
        [ProtoMember(1)]
        [Key(0)]
        public string Account { get; set; }
        [ProtoMember(2)]
        [Key(1)]
        public string Password { get; set; }
    }

    [MSGID(1004)]
    [ProtoContract]
    [MessagePackObject]
    public class Login2GateResult
    {
        [ProtoMember(1)]
        [Key(0)]
        public bool IsSuccess { get; set; }
    }
}
