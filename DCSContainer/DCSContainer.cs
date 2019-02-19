using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Megumin.Remote;
using Net.Remote;

namespace Megumin.DCS
{
    public partial class DCSContainer
    {
        protected readonly static DCSContainer Instance = new DCSContainer();

        public static IContainer MainContainer;

        int GUID = 0;
        private async Task<int> GetNewSeviceIDAsync()
        {
            return GUID++;
        }

        List<IPAddress> iPAddresses = new List<IPAddress>();
        Dictionary<int, IService> serviceDic = new Dictionary<int, IService>();
        public static async void AddService(IService service)
        {
            service.GUID = await Instance.GetNewSeviceIDAsync();
            Instance.serviceDic.Add(service.GUID, service);
            service.Start();
            //await Sockets.BroadCastAsync(new Login(), MainContainer.Sockets);
        }

        public static void Init()
        {
            
        }

        public static async Task Start()
        {
            Instance.iPAddresses.Add(IPAddress.IPv6Loopback);
            //IPAddress my = Instance.Remote.ConnectIPEndPoint.Address;
            //if (my == Instance.MainIP)
            //{
            //    if (CheckSocketPort(MainPort))
            //    {
            //        ///本机第一个进程
            //        Sockets.StartListen(MainPort);
            //        //Sockets.StopListen(MainPort);
            //    }
            //    else
            //    {
            //        ///本机其他进程，尝试分布间通讯
            //        var ex = await Sockets.ConnectAsync(MainIP, MainPort);
            //        if (ex == null)
            //        {
            //            ///成功连接，开始注册
            //            ///
            //            await Sockets.Send<TestMessage>(new byte[10]);
            //        }
            //    }
            //}
        }

        /// <summary>
        /// 检测端口是否可用，TCP,UDP同时可用返回true
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool CheckSocketPort(int port)
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();

            var tcpPort = ipProperties.GetActiveTcpListeners().FirstOrDefault(p => p.Port == port);
            if (tcpPort != null)
            {
                return false;
            }

            var udpPort = ipProperties.GetActiveUdpListeners().FirstOrDefault(p => p.Port == port);
            if (udpPort != null)
            {
                return false;
            }
            return true;
        }

        private DCSContainer() { }

        public IRemote Remote { get; private set; } = new TcpRemote();
        /// <summary>
        /// 起始端口
        /// </summary>
        public int MainPort { get; private set; } = 54321;
        /// <summary>
        /// 分布式中第一个默认IP
        /// </summary>
        public IPAddress MainIP { get; private set; } = IPAddress.IPv6Loopback;
    }


    public class TestMessage
    {

    }

    public class TestMessage2
    {

    }
}
