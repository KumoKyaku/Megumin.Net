using Megumin.Remote;
using Megumin.Remote.Simple;
using Megumin.Remote.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    [TestClass]
    public class UnitTestRemote2
    {
        private UdpRemote CreateUdp()
        {
            return new EchoUdp();
        }

        private KcpRemote CreateKcp()
        {
            return new EchoKcp();
        }

        [TestMethod]
        public void TestUdpRemote()
        {
            const int port = 65432;
            UdpRemoteListener listener = new UdpRemoteListener(port);
            listener.ListenAsync(CreateUdp);

            UdpRemote client = new UdpRemote();
            client.ConnectIPEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            client.ClientSideRecv(port - 1);
            EchoTest(client);
            listener.Stop();
        }


        [TestMethod]
        public void TestKcpRemote()
        {
            const int port = 55432;
            KcpRemoteListener listener = new KcpRemoteListener(port);
            listener.ListenAsync(CreateKcp);

            KcpRemote client = new KcpRemote();
            client.InitKcp(1001);
            client.ConnectIPEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            client.ClientSideRecv(port - 1);
            EchoTest(client);
            listener.Stop();
        }

        public void EchoTest(IRemote remote)
        {
            MessageLUT.Regist(new TestPacket1());
            MessageLUT.Regist(new TestPacket2());

            TestPacket1 packet1 = new TestPacket1() { Value = 5645645 };
            var ret = remote.SendSafeAwait<TestPacket1>(packet1, 
                options: SendOption.Never).ConfigureAwait(false)
                .GetAwaiter().GetResult();
            Assert.AreEqual(packet1.Value, ret.Value);
        }
    }
}
