using Megumin.Remote;
using Megumin.Remote.Test;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static TestConfig;
using System.Security.Cryptography;

namespace TestServer
{
    /// <summary>
    /// 
    /// </summary>
    internal class CoolDownTime
    {
        /// <summary>
        /// 是否冷却完毕
        /// </summary>
        public bool CoolDown
        {
            get
            {
                if (DateTime.Now - Last > MinDelta)
                {
                    Last = DateTime.Now;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 是否冷却完毕
        /// </summary>
        /// <param name="time"></param>
        public static implicit operator bool(CoolDownTime time)
        {
            return time.CoolDown;
        }


        /// <summary>
        /// 上次返回冷却完毕的时间
        /// </summary>
        public DateTime Last { get; set; } = DateTime.MinValue;
        /// <summary>
        /// 最小间隔
        /// </summary>
        public TimeSpan MinDelta { get; set; } = TimeSpan.FromMilliseconds(15);
    }


    class Program
    {
        const bool UsePost2ThreadScheduler = false;
        static void Main(string[] args)
        {
            MessageLUT.Regist(new TestPacket1());
            MessageLUT.Regist(new TestPacket2());
            Console.WriteLine($"服务器/Server----UsePost2ThreadScheduler:{UsePost2ThreadScheduler}");
            ListenAsync();
            Console.WriteLine($"客户端配置 RemoteCount:{RemoteCount}   MessageCount:{MessageCount}   TotalMessageCount:{RemoteCount * (long)MessageCount}");
            Console.ReadLine();
        }

        private static async void ListenAsync()
        {
            if (UsePost2ThreadScheduler)
            {
                ThreadPool.QueueUserWorkItem((A) =>
                {
                    CoolDownTime coolDown = new CoolDownTime() { MinDelta = TimeSpan.FromSeconds(30) };
                    while (true)
                    {
                        MessageThreadTransducer.Update(0);
                    }

                });
            }

            switch (PMode)
            {
                case Mode.TCP:
                    {
                        TcpRemoteListener remote = new TcpRemoteListener(54321);
                        Listen(remote);
                    }
                    break;
                case Mode.UDP:
                    {
                        UdpRemoteListener remote = new UdpRemoteListener(54321);
                        Listen(remote);
                    }
                    break;
                case Mode.KCP:
                    {
                        KcpRemoteListener remote = new KcpRemoteListener(54321);
                        Listen(remote);
                    }
                    break;
                default:
                    break;
            }
            
        }

        static int connectCount = 1;

        private static async void Listen(IListener<TcpRemote> remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(static ()=>
            {
                return new TestTcpServerRemote()
                {
                    Post2ThreadScheduler = UsePost2ThreadScheduler,
                    UID = connectCount
                };
            });
            Listen(remote);
            Console.WriteLine($"总接收到连接{connectCount++}");
        }

        private static async void Listen(IListener<UdpRemote> remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(static () =>
            {
                return new TestUdpServerRemote()
                {
                    Post2ThreadScheduler = UsePost2ThreadScheduler,
                    UID = connectCount
                };
            });
            Listen(remote);
            Console.WriteLine($"总接收到连接{connectCount++}");
        }

        private static async void Listen(IListener<KcpRemote> remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(static () =>
            {
                return new TestKcpServerRemote()
                {
                    Post2ThreadScheduler = UsePost2ThreadScheduler,
                    UID = connectCount
                };
            });
            Listen(remote);
            Console.WriteLine($"总接收到连接{connectCount++}");
        }
    }

    public sealed class TestTcpServerRemote : TcpRemote
    {
        static int totalCount;
        int myRecvCount = 0;
        protected async override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            totalCount++;
            myRecvCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    if (totalCount % 1 == 0)
                    {
                        Console.WriteLine($"Remote:{UID} 接收消息{nameof(TestPacket1)}--{packet1.Value}--MyRecvCount{myRecvCount}----总消息数{totalCount}");
                    }
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return packet2;
                default:
                    break;
            }
            return null;
        }
    }

    public sealed class TestUdpServerRemote : UdpRemote
    {
        static int totalCount;
        int myRecvCount = 0;
        protected async override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            totalCount++;
            myRecvCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    if (totalCount % 1 == 0)
                    {
                        Console.WriteLine($"Remote:{UID} 接收消息{nameof(TestPacket1)}--{packet1.Value}--MyRecvCount{myRecvCount}----总消息数{totalCount}");
                    }
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return packet2;
                default:
                    break;
            }
            return null;
        }
    }

    public sealed class TestKcpServerRemote : KcpRemote
    {
        static int totalCount;
        int myRecvCount = 0;
        protected async override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            totalCount++;
            myRecvCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    if (totalCount % 1 == 0)
                    {
                        Console.WriteLine($"Remote:{UID} 接收消息{nameof(TestPacket1)}--{packet1.Value}--MyRecvCount{myRecvCount}----总消息数{totalCount}");
                    }
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return packet2;
                default:
                    break;
            }
            return null;
        }
    }
}
