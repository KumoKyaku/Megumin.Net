using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Megumin;
using Megumin.Message;
using Megumin.Message.TestMessage;
using Megumin.Remote;
using Net.Remote;

namespace RemoteTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ListenAsync();
            //CoommonListen();
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        private static void CoommonListen()
        {
            Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.IPv6Any, 54321));
            bool IPV6 = Socket.OSSupportsIPv6;
            listener.Listen(10);
            var s = listener.Accept();
            Console.WriteLine("接收到连接");
        }

        private static async void ListenAsync()
        {
            ThreadPool.QueueUserWorkItem((A) =>
            {
                CoolDownTime coolDown = new CoolDownTime() {  MinDelta = TimeSpan.FromSeconds(30) };
                while (true)
                {
                    MessageThreadTransducer.Update(0);
                    //Thread.Sleep(1);
                    //if (coolDown)
                    //{
                    //    GC.Collect();
                    //}
                }

            });

            TcpRemoteListener remote = new TcpRemoteListener(54321);
            Listen(remote);
        }

        static int connectCount;

        private static async void Listen(TcpRemoteListener remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(TestFunction.DealMessage);
            Console.WriteLine($"接收到连接{connectCount++}");
            Listen(remote);
        }

        private static void TestConnect(IRemote re)
        {
            re.UID = connectCount;
            re.OnDisConnect += (er) =>
            {
                Console.WriteLine($"连接断开{re.UID}");
            };
        }
    }
}
