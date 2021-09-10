using Megumin.Remote;
using Megumin.Remote.Test;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static TestConfig;

public static class TestConfig
{
    public enum Mode
    {
        TCP, UDP, KCP
    }
    public static Mode PMode = TestConfig.Mode.UDP;
    public static int MessageCount = 1;
    public static int RemoteCount = 1;
}

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            MessageLUT.Regist(new TestPacket1());
            MessageLUT.Regist(new TestPacket2());
            Console.WriteLine("客户端/Client");
            ConAsync();
            Console.ReadLine();
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


        /// <summary>
        /// //峰值 12000 0000 字节每秒，平均 4~7千万字节每秒
        /// int MessageCount = 10000;
        /// int RemoteCount = 100;
        /// </summary>
        private static void TestSpeed()
        {
            for (int i = 1; i <= RemoteCount; i++)
            {
                NewRemote(i);
            }
        }

        private static async void NewRemote(int clientIndex)
        {
            IRemote remote = null;

            switch (PMode)
            {
                case Mode.TCP:
                    {
                        TestTcpRemote testR = new TestTcpRemote();
                        testR.Dealer.Index = clientIndex;
                        testR.Dealer.MessageCount = MessageCount;
                        remote = testR;
                    }
                    break;
                case Mode.UDP:
                    {
                        TestUdpRemote testR = new TestUdpRemote();
                        testR.Dealer.Index = clientIndex;
                        testR.Dealer.MessageCount = MessageCount;
                        remote = testR;
                    }
                    break;
                case Mode.KCP:
                    {
                        TestKcpRemote testR = new TestKcpRemote();
                        testR.Dealer.Index = clientIndex;
                        testR.Dealer.MessageCount = MessageCount;
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
                    await conn.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 54321));
                }
            }
            catch (Exception)
            {
                throw;
            }

            //await TestRpc(clientIndex, remote);

            Stopwatch look1 = new Stopwatch();
            var msg = new TestPacket1 { Value = 0 };
            look1.Start();

            await Task.Run(() =>
            {
                for (int i = 0; i < MessageCount; i++)
                {
                    msg.Value = i;
                    remote.Send(msg);
                }
            });

            look1.Stop();
            Console.WriteLine($"Remote{clientIndex}: SendAsync{MessageCount}包 ---- 用时: {look1.ElapsedMilliseconds}ms----- 平均每秒发送:{MessageCount * 1024 / (look1.ElapsedMilliseconds + 1)}");


            //Remote.BroadCastAsync(new Packet1 { Value = -99999 },remote);

            //var (Result, Excption) = await remote.SendAsync<Packet2>(new Packet1 { Value = 100 });
            //Console.WriteLine($"RPC接收消息{nameof(Packet2)}--{Result.Value}");
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

        #region 连接测试


        private static async void TestConnect()
        {
            for (int i = 0; i < RemoteCount; i++)
            {
                Connect(i);
            }
        }

        private static async void Connect(int index)
        {
            TcpRemote remote = new TcpRemote();
            try
            {
                await remote.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 54321));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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


