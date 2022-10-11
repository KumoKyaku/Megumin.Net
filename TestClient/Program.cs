using Megumin.Remote;
using Net.Remote;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static TestConfig;
using Megumin.Message;
using System.Collections.Generic;
using System.Linq;

public static class TestConfig
{
    public enum Mode
    {
        TCP, UDP, KCP
    }
    public static Mode PMode = TestConfig.Mode.UDP;
    public static int Port = 54321;
    public static int MessageCount = 0;
    /// <summary>
    /// 本机同时开Client Server 15000左右连接Tcp就会用尽端口
    /// Tcp每秒8kw字节差不多就是极限了，多了就自动卡死了，不清楚是socket问题还是console.writeline问题。
    /// Upd 测试5000连接比较稳定。
    /// Kcp测试10000连接没有成功。5000也不性。推测应该是UdpListenner一个端口无法处理这么大流量，大量丢包。
    /// </summary>
    public static int RemoteCount = 1;
}

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("客户端/Client");
            ConAsync();
            while (true)
            {
                var key = Console.ReadKey();
                OnKey(key);
            }
        }

        private static async void ConAsync()
        {
            //ThreadPool.QueueUserWorkItem((A) =>
            //{
            //    while (true)
            //    {
            //        MessageThreadTransducer.Update(0);
            //        //Thread.Yield();
            //    }

            //});

            ///性能测试
            TestSpeed();
            ///连接测试
            //TestConnect();
        }


        #region 性能测试

        static Dictionary<int, IRemote> AllClient = new Dictionary<int, IRemote>();

        private static void OnKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.F1:
                    SendAMessage();
                    break;
                case ConsoleKey.F2:
                    SendAMessage2();
                    break;
                case ConsoleKey.F4:
                    SendTestMessage();
                    break;
                default:
                    break;
            }
        }

        private static async void SendTestMessage()
        {
            var msg = new TestPacket1 { Value = 0 };
            await Task.Run(() =>
            {
                foreach (var remote in AllClient)
                {
                    Stopwatch look1 = new Stopwatch();
                    look1.Start();
                    for (int i = 0; i < MessageCount; i++)
                    {
                        msg.Value = i;
                        remote.Value?.Send(msg);
                    }
                    look1.Stop();
                    Console.WriteLine($"Remote{remote.Key}: SendAsync{MessageCount}包 ---- 用时: {look1.ElapsedMilliseconds}ms----- 平均每秒发送:{MessageCount * 1024 / (look1.ElapsedMilliseconds + 1)}");
                }
            });
        }

        private static void SendAMessage2()
        {
            var msg = new TestPacket3();
            foreach (var item in AllClient)
            {
                item.Value?.Send(msg);
            }
        }

        private static void SendAMessage()
        {
            var msg = new TestPacket4();
            foreach (var item in AllClient)
            {
                item.Value?.Send(msg, SendOption.Echo);
            }
        }

        /// <summary>
        /// //峰值 12000 0000 字节每秒，平均 4~7千万字节每秒
        /// int MessageCount = 10000;
        /// int RemoteCount = 100;
        /// </summary>
        private static async void TestSpeed()
        {
            for (int i = 1; i <= RemoteCount; i++)
            {
                await NewRemote(i);
                if (i % 10 == 0)
                {
                    //连接太快了
                    await Task.Delay(30);
                }
            }

            SendTestMessage();
        }

        private static async Task NewRemote(int clientIndex)
        {
            IRemote remote = null;

            switch (PMode)
            {
                case Mode.TCP:
                    {
                        TestTcpRemote testR = new TestTcpRemote();
                        testR.Dealer.Index = clientIndex;
                        testR.Dealer.MessageCount = MessageCount;
                        testR.TraceListener = new ConsoleTraceListener();
                        remote = testR;
                    }
                    break;
                case Mode.UDP:
                    {
                        TestUdpRemote testR = new TestUdpRemote();
                        testR.Dealer.Index = clientIndex;
                        testR.Dealer.MessageCount = MessageCount;
                        testR.TraceListener = new ConsoleTraceListener();
                        remote = testR;
                    }
                    break;
                case Mode.KCP:
                    {
                        TestKcpRemote testR = new TestKcpRemote();
                        testR.Dealer.Index = clientIndex;
                        testR.Dealer.MessageCount = MessageCount;
                        testR.TraceListener = new ConsoleTraceListener();
                        remote = testR;
                    }
                    break;
                default:
                    break;
            }

            try
            {
                if (remote is IConnectable conn)
                {
                    await conn.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port));
                    Console.WriteLine($"Remote{clientIndex} 连接成功。");
                    if (remote is KcpRemote kcp)
                    {
                        //kcp.KcpCore.TraceListener = new ConsoleTraceListener();
                        kcp.SendBeat();
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            AllClient[clientIndex] = remote;
        }

        private static async Task TestRpc(int clientIndex, IRemote remote)
        {
            var res2 = await remote.SendSafeAwait<TestPacket2>(new TestPacket2() { Value = clientIndex },
                            (ex) =>
                            {
                                if (ex is TimeoutException timeout)
                                {
                                    Console.WriteLine($"Rpc调用超时----------------------------------------- {clientIndex}");
                                }
                                else
                                {
                                    Console.WriteLine($"Rpc调用异常--------------------{ex}------------- {clientIndex}");
                                }
                            });
            Console.WriteLine($"Rpc调用返回----------------------------------------- {res2.Value}");
        }

        #endregion
    }

    public class Dealer
    {
        public int Index { get; set; }
        public int MessageCount { get; set; }
        Stopwatch stopwatch = new Stopwatch();

        public async ValueTask<object>
            OnReceive(short cmd, int messageID, object message)
        {
            switch (message)
            {
                case TestPacket1 packet1:
                    Console.WriteLine($"Remote{Index}:接收消息{nameof(TestPacket1)}--{packet1.Value}");
                    return new TestPacket2 { Value = packet1.Value };
                case TestPacket2 packet2:
                    Console.WriteLine($"Remote{Index}:接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    if (packet2.Value == 0)
                    {
                        stopwatch.Restart();
                    }
                    if (packet2.Value == MessageCount - 1)
                    {
                        stopwatch.Stop();

                        Console.WriteLine($"Remote{Index}:TestReceive{MessageCount} ------ {stopwatch.ElapsedMilliseconds}----- 每秒:{MessageCount * 1000 / (stopwatch.ElapsedMilliseconds + 1)}");
                    }
                    return null;
                default:
                    Console.WriteLine($"Remote{Index}:接收消息{message.GetType().Name}");
                    break;
            }
            return null;
        }
    }

    public sealed class TestTcpRemote : TcpRemote
    {
        public Dealer Dealer = new Dealer();
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message) => Dealer.OnReceive(cmd, messageID, message);
    }

    public sealed class TestUdpRemote : UdpRemote
    {
        public Dealer Dealer = new Dealer();
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message) => Dealer.OnReceive(cmd, messageID, message);
    }

    public sealed class TestKcpRemote : KcpRemote
    {
        public Dealer Dealer = new Dealer();
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message) => Dealer.OnReceive(cmd, messageID, message);
    }
}


