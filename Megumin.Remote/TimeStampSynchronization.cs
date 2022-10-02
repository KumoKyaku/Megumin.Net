using Megumin.Message;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    /// <summary>
    /// 时间戳同步
    /// </summary>
    public class TimeStampSynchronization
    {
        public TimeSpan Offset { get; protected set; } = TimeSpan.Zero;
        public int OffsetMilliseconds { get; protected set; } = 0;
        public int RttMilliseconds { get; protected set; } = 0;
        public DateTimeOffset RemoteUtcNow => DateTimeOffset.UtcNow + Offset;
        public OffsetValue Min;
        public OffsetValue Max;
        public bool DebugLog = false;

        protected void Reset()
        {
            Offset = TimeSpan.Zero;
            OffsetMilliseconds = default;
            RttMilliseconds = default;
            Min = default;
            Max = default;
        }

        public struct OffsetValue
        {
            public int OffsetMilliseconds;
            public int RttMilliseconds;
            public int Index;

            public override string ToString()
            {
                return $"{{Index:{Index}  Offset:{OffsetMilliseconds}  Rtt:{RttMilliseconds}}}";
            }
        }

        /// <summary>
        /// 实测受到延迟和丢包率影响。延迟对结果影响不大。丢包率在5%误差100ms左右
        /// <para>count 10-20 interval 50ms-100比较合适</para>
        /// </summary>
        /// <param name="remote"></param>
        /// <param name="count"></param>
        /// <param name="interval"></param>
        /// <returns></returns>
        public async ValueTask Sync(ISendCanAwaitable remote, int count = 20, int interval = 50)
        {
            List<Task<OffsetValue>> tasks = new List<Task<OffsetValue>>();
            tasks.Clear();
            for (int i = 0; i < count; i++)
            {
                tasks.Add(GetOffset(remote, i));
                await Task.Delay(interval);
            }

            await Task.WhenAll(tasks);
            List<OffsetValue> offsets = new List<OffsetValue>();
            foreach (var item in tasks)
            {
                if (item.Result.OffsetMilliseconds >= 0)
                {
                    offsets.Add(item.Result);
                }
            }

            if (offsets.Count == 0)
            {
                Reset();
                return;
            }

            offsets.Sort((l, r) => { return l.OffsetMilliseconds.CompareTo(r.OffsetMilliseconds); });
            Min = offsets.First();
            Max = offsets.Last();
            var totalResultCount = offsets.Count;

            if (offsets.Count > 2)
            {
                //去掉一个最大值一个最小值
                offsets.RemoveAt(offsets.Count - 1);
                offsets.RemoveAt(0);
            }

            var offset = (int)offsets.Average(ele => ele.OffsetMilliseconds);
            OffsetMilliseconds = offset;
            Offset = TimeSpan.FromMilliseconds(offset);
            RttMilliseconds = (int)offsets.Average(ele => ele.RttMilliseconds);
            if (DebugLog)
            {
                Log($"AverageOffset:{OffsetMilliseconds}  AverageRtt:{RttMilliseconds}  TotalResultCount:{totalResultCount} Min:{Min} Max:{Max}");
            }
        }

        protected static readonly SendOption sendOption = new SendOption()
        {
            MillisecondsDelay = 2000,
            RpcComplatePost2ThreadScheduler = false,
        };

        public async Task<OffsetValue> GetOffset(ISendCanAwaitable remote, int index = 0)
        {
            SendOption sendOption = new SendOption()
            {
                MillisecondsDelay = 2000,
                RpcComplatePost2ThreadScheduler = false,
            };
            var sendTime = DateTimeOffset.UtcNow;
            var (remotetime, ex) = await remote.Send<DateTimeOffset>(new GetTime(), options: sendOption).ConfigureAwait(false);

            var loacalUtcNow = DateTimeOffset.UtcNow;
            var rttSpan = loacalUtcNow - sendTime;
            var rtt = (int)rttSpan.TotalMilliseconds;
            OffsetValue result = new OffsetValue();
            result.OffsetMilliseconds = -1;
            result.RttMilliseconds = rtt;
            result.Index = index;

            if (ex == null)
            {

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                var calRemoteUtcNow = remotetime + (rttSpan / 2);
#else
                var calRemoteUtcNow = remotetime + (TimeSpan.FromMilliseconds(rttSpan.TotalMilliseconds / 2));
#endif

                // LoacalUtcNow + Offset = RemoteUtcNow
                var offsetSpan = calRemoteUtcNow - loacalUtcNow;
                result.OffsetMilliseconds = (int)offsetSpan.TotalMilliseconds;
            }

            if (DebugLog)
            {
                Log($"测试UtcNow index:{result.Index} offset:{result.OffsetMilliseconds} rtt:{result.RttMilliseconds}");
            }
            return result;
        }

        public virtual void Log(object obj)
        {
            //Debug.Log(obj);
        }
    }
}
