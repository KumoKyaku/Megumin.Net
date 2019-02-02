using System;
using System.Buffers;
using System.IO;
using Megumin.Message;
using Message;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestMessage
    {
        [TestMethod]
        public void TestMethod1()
        {
            Login2Gate login2Gate = new Login2Gate()
            {
                Account = "test",
                Password = "123456"
            };

            {
                var b = MessagePack.MessagePackSerializer.Serialize(login2Gate);
                var res = MessagePack.MessagePackSerializer.Deserialize<Login2Gate>(b);
                Assert.AreEqual(login2Gate.Account, res.Account);
                Assert.AreEqual(login2Gate.Password, res.Password);
            }

            {
                using (MemoryStream ms = new MemoryStream(1024))
                {
                    Serializer.Serialize(ms, login2Gate);
                    ms.Seek(0, SeekOrigin.Begin);
                    var res = Serializer.Deserialize<Login2Gate>(ms);
                    Assert.AreEqual(login2Gate.Account, res.Account);
                    Assert.AreEqual(login2Gate.Password, res.Password);
                }

                Protobuf_netLUT.Regist(typeof(Login2Gate).Assembly);
                using (var buffer = BufferPool.Rent(1024))
                {
                    var length = MessageLUT.Serialize(login2Gate, buffer.Memory.Span);
                    var res = MessageLUT.Deserialize(1003, buffer.Memory.Slice(0,length.length)) as Login2Gate;
                    Assert.AreEqual(login2Gate.Account, res.Account);
                    Assert.AreEqual(login2Gate.Password, res.Password);
                }

            }
        }
    }
}
