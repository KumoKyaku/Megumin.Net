using System.Collections;
using System.Collections.Generic;
using Megumin;
using Megumin.Message;
using Megumin.Remote;
using ProtoBuf;
using UnityEngine;

[MSGID(1001)]
[ProtoContract]
public class Login
{
    [ProtoMember(1)]
    public string IP { get; set; }
}

[MSGID(1002)]
public class LoginResult
{
    [ProtoMember(1)]
    public string TempKey { get; set; }
}

public class Message : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [Button]
    public void Test()
    {
        Protobuf_netLUT.Regist<Login>();
        Protobuf_netLUT.Regist<LoginResult>();

        Login login = new Login() { IP = "test" };
        MessageLUTTestBuffer buffer = new MessageLUTTestBuffer();
        var msgid = Protobuf_netLUT.Serialize(buffer,login);
        var login2 = Protobuf_netLUT.Deserialize(msgid, buffer.ReadOnlySequence) as Login;
        Debug.Log($"{msgid}--{login.IP}--{login2.IP}");
    }
}
