using Microsoft.VisualStudio.TestTools.UnitTesting;
using Megumin.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Megumin.Message;
using System.Buffers;

namespace Megumin.Remote.Tests
{
    [TestClass()]
    public class MessageLUTTests
    {
        [TestMethod()]
        public void DeserializeTest()
        {
            Test("TestStr");
            Test(200);
            Test(200.01f);
            Test(double.MaxValue);
            Test(DateTimeOffset.UtcNow);

            GetTime getTime = new GetTime();
            var dobj = MessageLUT.TestType(getTime);
            Assert.AreEqual(getTime.PreReceiveType, dobj.PreReceiveType);

            TestPacket3 testPacket3 = new TestPacket3() { Value = 200 };
            var dtestPacket3 = MessageLUT.TestType(testPacket3);
            Assert.AreEqual(testPacket3.Value, dtestPacket3.Value);
        }

        private static void Test<T>(T original)
        {
            var dv = MessageLUT.TestType(original);
            Assert.AreEqual(original, dv);
        }
    }
}