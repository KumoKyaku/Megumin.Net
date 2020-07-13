using Megumin.DCS;
using Megumin.Message;
using Megumin.Message.Test;
using System;
using System.Threading;

namespace DemoServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            InitServer();
            Console.ReadLine();
        }

        private static async void InitServer()
        {
            //MessagePackLUT.Regist(typeof(Login).Assembly);
            Protobuf_netLUT.Regist(typeof(Login).Assembly);
            ThreadPool.QueueUserWorkItem((A) =>
            {
                while (true)
                {
                    MessageThreadTransducer.Update(0);
                    Thread.Yield();
                }

            });

            //FightService service = new FightService();
            //BusinessContainer.Instance.AddService(service);
            GateService gateService = new GateService();
            DCSContainer.AddService(gateService);
        }
    }

    /// <summary>
    /// 一个容器中可以拥有多个服务，每个服务包含多个System，每个Syetem依赖于组件，实体的组建在多个服务的多个System中共享
    /// </summary>
    public enum ServiceType
    {
        /// <summary>
        /// 战斗服务，攻击移动
        /// </summary>
        Fight,
        /// <summary>
        /// 装备，交易等（药水的使用视为释放了一个加血技能，由战斗服务器结算）
        /// </summary>
        Item,
        DB,
        /// <summary>
        /// 邮件服务
        /// </summary>
        Mail,
        /// <summary>
        /// 实例同步服务（待定）
        /// </summary>
        Entity,
        Gate,
    }
}
