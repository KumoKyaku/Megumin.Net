using Megumin.Message;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static TestConfig;

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
            Console.WriteLine($"服务器/Server----UsePost2ThreadScheduler:{UsePost2ThreadScheduler}");
            ListenAsync();
            Console.WriteLine($"客户端配置 {PMode} RemoteCount:{RemoteCount}   MessageCount:{MessageCount}   TotalMessageCount:{RemoteCount * (long)MessageCount}");
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

            bool useNewListener = true;
            if (useNewListener)
            {
                switch (PMode)
                {
                    case Mode.TCP:
                        {
                            TcpRemoteListener listener2 = new TcpRemoteListener(Port);
                            listener2.TraceListener = new ConsoleTraceListener();
                            listener2.Start();
                            while (true)
                            {
                                TestServerRemote re = new TestServerRemote() { UID = connectCount };
                                var trans = new TcpTransport();
                                re.SetTransport(trans);
                                /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
                                await listener2.ReadAsync(trans).ConfigureAwait(false);
                                Console.WriteLine($"总接收到连接{connectCount}");
                                Interlocked.Increment(ref connectCount);
                            }
                        }
                        break;
                    case Mode.UDP:
                        {
                            UdpRemoteListener listener2 = new UdpRemoteListener(Port);
                            listener2.TraceListener = new ConsoleTraceListener();
                            listener2.Start();
                            while (true)
                            {
                                TestServerRemote re = new TestServerRemote() { UID = connectCount };
                                var trans = new UdpTransport();
                                re.SetTransport(trans);
                                await listener2.ReadAsync(trans).ConfigureAwait(false);
                                Console.WriteLine($"总接收到连接{connectCount}");
                                Interlocked.Increment(ref connectCount);
                            }
                        }
                        break;
                    case Mode.KCP:
                        {
                            KcpRemoteListener listener2 = new KcpRemoteListener(Port);
                            listener2.TraceListener = new ConsoleTraceListener();
                            listener2.Start();
                            while (true)
                            {
                                TestServerRemote re = new TestServerRemote() { UID = connectCount };
                                var trans = new KcpTransport();
                                re.SetTransport(trans);
                                await listener2.ReadAsync(trans).ConfigureAwait(false);
                                Console.WriteLine($"总接收到连接{connectCount}");
                                //re.KcpCore.TraceListener = new ConsoleTraceListener();
                                Interlocked.Increment(ref connectCount);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                //switch (PMode)
                //{
                //    case Mode.TCP:
                //        {
                //            TcpRemoteListenerOld remote = new TcpRemoteListenerOld(Port);
                //            remote.TraceListener = new ConsoleTraceListener();
                //            Listen(remote);
                //        }
                //        break;
                //    case Mode.UDP:
                //        {
                //            UdpRemoteListenerOld remote = new UdpRemoteListenerOld(Port);
                //            remote.TraceListener = new ConsoleTraceListener();
                //            Listen(remote);
                //        }
                //        break;
                //    case Mode.KCP:
                //        {
                //            KcpRemoteListenerOld remote = new KcpRemoteListenerOld(Port);
                //            remote.TraceListener = new ConsoleTraceListener();
                //            ListenKcp(remote);
                //        }
                //        break;
                //    default:
                //        break;
                //}
            }
        }

        static int connectCount = 1;

        //private static async void Listen(IListenerOld<TcpRemote> remote)
        //{
        //    /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
        //    var re = await remote.ListenAsync(static () =>
        //    {
        //        Console.WriteLine($"总接收到连接{connectCount}");
        //        return new TestTcpServerRemote()
        //        {
        //            Post2ThreadScheduler = UsePost2ThreadScheduler,
        //            UID = connectCount,
        //            TraceListener = new ConsoleTraceListener(),
        //        };
        //    });
        //    Interlocked.Increment(ref connectCount);
        //    Listen(remote);
        //}

        //private static async void Listen(IListenerOld<UdpRemote> remote)
        //{
        //    /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
        //    var re = await remote.ListenAsync(static () =>
        //    {
        //        Console.WriteLine($"总接收到连接{connectCount}");
        //        return new TestUdpServerRemote()
        //        {
        //            Post2ThreadScheduler = UsePost2ThreadScheduler,
        //            UID = connectCount,
        //            TraceListener = new ConsoleTraceListener(),
        //        };
        //    });
        //    Interlocked.Increment(ref connectCount);
        //    Listen(remote);
        //}

        //private static async void ListenKcp(IListenerOld<KcpRemote> remote)
        //{
        //    /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
        //    var re = await remote.ListenAsync(static () =>
        //    {
        //        Console.WriteLine($"总接收到连接{connectCount}");
        //        return new TestKcpServerRemote()
        //        {
        //            Post2ThreadScheduler = UsePost2ThreadScheduler,
        //            UID = connectCount,
        //            TraceListener = new ConsoleTraceListener(),
        //        };
        //    });
        //    //re.KcpCore.TraceListener = new ConsoleTraceListener();
        //    Interlocked.Increment(ref connectCount);
        //    ListenKcp(remote);
        //}
    }

    public sealed class TestServerRemote : UniversalRemote
    {
        static int totalCount;
        int myRecvCount = 0;
        public async override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Interlocked.Increment(ref totalCount);
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
                    Console.WriteLine($"Remote{UID}:接收消息{message.GetType().Name}");
                    break;
            }
            return null;
        }
    }
}
