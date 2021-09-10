using System;
using System.Net;
using System.Net.Sockets;

namespace SocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Socket[] sockets = new Socket[100000];
            for (int i = 0; i < 100000; i++)
            {
                var socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                sockets[i] = socket;
            }

            TestLife();

            Console.WriteLine("start");
            Console.ReadLine();
            GC.Collect();
            GC.Collect(0, GCCollectionMode.Forced);
            GC.Collect(1, GCCollectionMode.Forced);
            GC.Collect(2, GCCollectionMode.Forced);
            Console.WriteLine("gc");
            Console.ReadLine();
        }

        private static void TestLife()
        {
            byte[] cache = new byte[10000];
            Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += (sender, e) => { };
            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 54321));
            socket.Listen();
            socket.AcceptAsync(e);
            //socket.Close();
        }
    }
}
