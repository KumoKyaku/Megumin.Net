using Megumin.Message;
using Megumin.Remote;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Net.Remote;
using System.Net;

namespace UnitTest
{
    [TestClass]
    public class UnitTestRemote2
    {
        private UdpRemote CreateUdp()
        {
            return new UdpRemote();
        }

        private KcpRemote CreateKcp()
        {
            return new KcpRemote();
        }

        //[TestMethod]
        public void TestUdpRemote()
        {
            const int port = 65432;
            UdpRemoteListenerOld listener = new UdpRemoteListenerOld(port);
            listener.ListenAsync(CreateUdp);

            UdpRemote client = new UdpRemote();
            client.ConnectIPEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            client.ClientSideSocketReceive();
            EchoTest(client);
            listener.Stop();
        }


        //[TestMethod]
        public void TestKcpRemote()
        {
            const int port = 55432;
            KcpRemoteListenerOld listener = new KcpRemoteListenerOld(port);
            listener.ListenAsync(CreateKcp);

            KcpRemote client = new KcpRemote();
            client.InitKcp(1001);
            client.ConnectIPEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            client.ClientSideSocketReceive();
            EchoTest(client);
            listener.Stop();
        }

        public void EchoTest(IRemote remote)
        {
            TestPacket1 packet1 = new TestPacket1() { Value = 5645645 };
            var ret = remote.SendSafeAwait<TestPacket1>(packet1, 
                options: SendOption.Echo).ConfigureAwait(false)
                .GetAwaiter().GetResult();
            Assert.AreEqual(packet1.Value, ret.Value);
        }
    }
}
