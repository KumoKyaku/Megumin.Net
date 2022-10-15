using Megumin.Remote;
using Megumin.Remote.Test;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Net;
using System.Threading;

namespace DemoClient
{
    class Program
    {
        static void Main(string[] args)
        {
            ///将协议类的程序集注册进查找表中
            MessagePackLUT.Regist(typeof(Login).Assembly);

            ///建立主线程 或指定的任何线程 轮询。（确保在unity中使用主线程轮询）
            ///ThreadScheduler保证网络底层的各种回调函数切换到主线程执行以保证执行顺序。
            ThreadPool.QueueUserWorkItem((A) =>
            {
                while (true)
                {
                    MessageThreadTransducer.Update(0);
                    Thread.Yield();
                }

            });

            ConnectAsync();
            Console.ReadLine();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        private static async void ConnectAsync()
        {
            TcpRemote remote = new TcpRemote();
            try
            {
                await remote.ConnectAsync(new IPEndPoint(IPAddress.IPv6Loopback, 54321));

            }
            catch (Exception ex)
            {
                ///连接失败
                Console.WriteLine(ex.ToString());
                return;
            }

            ///没有异常，连接成功
            Console.WriteLine("连接成功");

            ///创建一个登陆消息
            var login = new Login2Gate
            {
                Account = $"TestClient",
                Password = "123456"
            };

            ///有返回值，这个是一个RPC过程，Exception在网络中传递
            //var resp = await remote.SendSafeAwait<Login2GateResult>(login);
            //if (resp.IsSuccess)
            //{
            //    Console.WriteLine("登陆成功");
            //}

            ///没有返回值，不是RPC过程
        }
    }
}
