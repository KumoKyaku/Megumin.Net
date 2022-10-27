using Megumin.Remote;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Megumin.Remote.Tests
{
    [TestClass()]
    public class UnitTest1
    {
        [TestMethod()]
        public void PreReceiveTest()
        {
            string IntegerToBinaryString(short theNumber)
            {
                string v = Convert.ToString(theNumber, 2).PadLeft(16, '0');
                v = v.Insert(12, "_");
                v = v.Insert(8, "_");
                v = v.Insert(4, "_");
                return $"0b{v}";
            }

            short cmd = 0;
            var str = "0b0000_0000_0000_0000";
            cmd = 1 << 0;
            str = IntegerToBinaryString(cmd);
            Assert.AreEqual(1, cmd);
            Assert.AreEqual(true, (cmd & 0b0000_0000_0000_0001) != 0);
            Assert.AreEqual("0b0000_0000_0000_0001", str);

            cmd = 1 << 1;
            str = IntegerToBinaryString(cmd);
            Assert.AreEqual(2, cmd);
            Assert.AreEqual(true, (cmd & 0b0000_0000_0000_0010) != 0);
            Assert.AreEqual("0b0000_0000_0000_0010", str);

            cmd = 1 << 3;
            str = IntegerToBinaryString(cmd);
            Assert.AreEqual(8, cmd);
            Assert.AreEqual(true, (cmd & 0b0000_0000_0000_1000) != 0);
            Assert.AreEqual("0b0000_0000_0000_1000", str);

            cmd = 1 << 14;
            str = IntegerToBinaryString(cmd);
            Assert.AreEqual(Math.Pow(2, 14), cmd);
            Assert.AreEqual(true, (cmd & 0b0100_0000_0000_0000) != 0);
            Assert.AreEqual("0b0100_0000_0000_0000", str);

            unchecked
            {
                cmd = (short)(1 << 15);
            }
            Assert.AreEqual((short)(Math.Pow(2, 15)), cmd);
            str = IntegerToBinaryString(cmd);
            Assert.AreEqual(true, (cmd & 0b1000_0000_0000_0000) != 0);
            Assert.AreEqual("0b1000_0000_0000_0000", str);

            cmd = -1;
            str = IntegerToBinaryString(cmd);
            Assert.AreEqual(true, (cmd & 0b1111_1111_1111_1111) != 0);
            Assert.AreEqual("0b1111_1111_1111_1111", str);
        }
    }
}

namespace UnitTest
{
    [TestClass]
    public class UnitTest1
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
        }
    }
}



