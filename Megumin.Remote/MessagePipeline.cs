using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Message
{
    //public class TestFunction
    //{
    //    static int totalCount = 0;
    //    public static async ValueTask<object> DealMessage(object message,IReceiveMessage receiver)
    //    {
    //        totalCount++;
    //        switch (message)
    //        {
    //            case TestPacket1 packet1:
    //                if (totalCount % 100 == 0)
    //                {
    //                    Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}------总消息数{totalCount}"); 
    //                }
    //                return null;
    //            case TestPacket2 packet2:
    //                Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
    //                return new TestPacket2 { Value = packet2.Value };
    //            default:
    //                break;
    //        }
    //        return null;
    //    }
    //}
}
