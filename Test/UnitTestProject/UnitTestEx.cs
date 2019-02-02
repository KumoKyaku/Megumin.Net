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
        public void TestRemoveAllInt()
        {
            Dictionary<int, int> test = new Dictionary<int, int>();
            test.Add(1, 1);
            test.Add(2, 2);
            test.Add(3, 2);
            test.Add(4, 3);

            Func<KeyValuePair<int,int>,bool> predicate = (kv) =>
            {
                return kv.Value >= 2;
            };

            test.RemoveAll(predicate);
            Assert.AreEqual(false,test.Any(predicate));
        }

        [TestMethod]
        public void TestRemoveAllString()
        {
            Dictionary<string, int> test = new Dictionary<string, int>();
            test.Add("1", 1);
            test.Add("2", 2);
            test.Add("3", 2);
            test.Add("4", 3);

            Func<KeyValuePair<string, int>, bool> predicate = (kv) =>
              {
                  return kv.Value >= 2;
              };

            test.RemoveAll(predicate);
            Assert.AreEqual(false, test.Any(predicate));
        }

        [TestMethod]
        public void TestWaitAsync()
        {
            Wait().Wait();
        }

        private static async Task Wait()
        {
            var c = await Task.Delay(100).WaitAsync(150);
            Assert.AreEqual(true, c);
            var c2 = await Task.Delay(200).WaitAsync(150);
            Assert.AreEqual(false, c2);
        }

        [TestMethod]
        public void TestLazyTask()
        {
            
        }

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
