using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using Megumin.Message;
using Message;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestMessage
    {
        static Login2Gate login2Gate = new Login2Gate()
        {
            Account = "test",
            Password = "123456"
        };

        [TestMethod]
        public void TestMessageLUT()
        {
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
            }
        }

        [TestMethod]
        public void TestProtobuf_netLUT()
        {
            Protobuf_netLUT.Regist(typeof(Login2Gate).Assembly);
            var pipe = new Pipe();
            MessageLUT.Serialize(pipe.Writer, login2Gate);
            pipe.Writer.Complete();
            pipe.Reader.TryRead(out var readResult);
            var res = MessageLUT.Deserialize(1003, readResult.Buffer) as Login2Gate;
            Assert.AreEqual(login2Gate.Account, res.Account);
            Assert.AreEqual(login2Gate.Password, res.Password);
        }
    }
}
