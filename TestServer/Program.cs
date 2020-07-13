using Megumin.Message;
using Megumin.Message.Test;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
        static void Main(string[] args)
        {
            MessageLUT.Regist(new TestPacket1());
            MessageLUT.Regist(new TestPacket2());

            ListenAsync();
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        private static async void ListenAsync()
        {
            ThreadPool.QueueUserWorkItem((A) =>
            {
                CoolDownTime coolDown = new CoolDownTime() { MinDelta = TimeSpan.FromSeconds(30) };
                while (true)
                {
                    MessageThreadTransducer.Update(0);
                }

            });

            TcpRemoteListener remote = new TcpRemoteListener(54321);
            Listen(remote);
        }

        static int connectCount;

        private static async void Listen(TcpRemoteListener remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(Create);
            Listen(remote);
            Console.WriteLine($"接收到连接{connectCount++}");
        }

        public static TestSpeedServerRemote Create()
        {
            return new TestSpeedServerRemote() { Post2ThreadScheduler = true };
        }
    }


    public class TestSpeedServerRemote:TcpRemote
    {
        protected override ValueTask<object> OnReceive(object message)
        {
            switch (message)
            {
                default:
                    break;
            }
            return NullResult;
        }
    }
}
