using Megumin.Remote;
using Megumin.Remote.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using System;
using System.IO;
using System.IO.Pipelines;

namespace UnitTest
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
            TestLogin2Gate();
        }

        [TestMethod]
        public void TestMessagePackLUT()
        {
            MessagePackLUT.Regist(typeof(Login2Gate).Assembly);
            TestLogin2Gate();
        }

        [TestMethod]
        public void TestProtobufLUT()
        {
            //todo
        }

        private static void TestLogin2Gate()
        {
            var pipe = new Pipe();
            MessageLUT.Serialize(pipe.Writer, login2Gate);
            pipe.Writer.Complete();
            pipe.Reader.TryRead(out var readResult);
            var res = MessageLUT.Deserialize(1003, readResult.Buffer) as Login2Gate;
            Assert.AreEqual(login2Gate.Account, res.Account);
            Assert.AreEqual(login2Gate.Password, res.Password);
        }


        [TestMethod]
        public void TestUDPAuth()
        {
            UdpAuthRequest auth = new UdpAuthRequest();
            auth.Guid = new Guid();
            var buffer = new byte[50];
            auth.Serialize(buffer);
            var ret = UdpAuthRequest.Deserialize(buffer);
            Assert.AreEqual(auth, ret);
        }

        [TestMethod]
        public void TestUDPAnswer()
        {
            UdpAuthResponse answer = new UdpAuthResponse();
            answer.Guid = new Guid();
            answer.Password = new Random().Next();
            var buffer = new byte[50];
            answer.Serialize(buffer);
            var ret = UdpAuthResponse.Deserialize(buffer);
            Assert.AreEqual(answer, ret);
        }
    }
}
