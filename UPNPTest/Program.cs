using Open.Nat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UPNPTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //TestNAT();
            MapTest();
            Console.ReadLine();
        }

        private static async void MapTest()
        {
            var discoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(10000);
            var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 54321, 54321, "测试映射"));
            Console.WriteLine("MapingEnd");
            var mapping = await device.GetAllMappingsAsync();
            foreach (var item in mapping)
            {
                Console.WriteLine($"PublicIP:{item.PublicIP}--PublicPort:{item.PublicPort}--PrivateIP:{item.PrivateIP}--PrivatePort:{item.PrivatePort}--{item.Protocol}--{item.Description}");
            }
        }

        private static async void TestNAT()
        {
            var discoverer = new NatDiscoverer();
            var device = await discoverer.DiscoverDeviceAsync();
            var ip = await device.GetExternalIPAsync();
            Console.WriteLine("The external IP Address is: {0} ", ip);
        }
    }
}
