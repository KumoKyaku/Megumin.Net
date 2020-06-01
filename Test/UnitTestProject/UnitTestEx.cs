using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Megumin;
using Megumin.Remote;
using System.Buffers;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestEx
    {
        [TestMethod]
        public void TestBYTE()
        {
            byte a = 255;
            sbyte b = (sbyte)a;
            Assert.AreEqual(-1, b);
            a = 0;
            b = (sbyte)a;
            Assert.AreEqual(0, b);
            a = 1;
            b = (sbyte)a;
            Assert.AreEqual(1, b);
            byte[] buffer = new byte[4];
            255.WriteTo(buffer);
            256.WriteTo(buffer);
            65535.WriteTo(buffer);
            65536.WriteTo(buffer);
        }
    }
}
