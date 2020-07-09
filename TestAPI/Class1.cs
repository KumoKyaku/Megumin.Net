using System;
using System.Net.Sockets;

namespace TestAPI
{
    public class Class1
    {
        Socket Client { get; }
        public void ReceiveStart()
        {
            Client.ReceiveAsync
        }
    }
}
