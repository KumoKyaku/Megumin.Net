using Megumin.Message;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    public static class RemoteExtension
    {
        /// <summary>
        /// 测试往返时间。
        /// <para></para>
        /// 这里不用ConfigureAwait(false);将线程调度消耗的时间计算在Rtt内。
        /// </summary>
        /// <param name="send"></param>
        /// <returns>
        /// 往返时间毫秒数。
        /// 负数表示无法联通。
        /// </returns>
        public static async ValueTask<int> Rtt(this ISendAsyncable send)
        {
            if (send != null)
            {
                DateTimeOffset sendTime = DateTimeOffset.UtcNow;
                var (obj, ex) = await send.SendAsync<Heartbeat>(Heartbeat.Default, options: SendOption.Echo);
                if (ex == null)
                {
                    DateTimeOffset respTime = DateTimeOffset.UtcNow;
                    var rtt = (int)((respTime - sendTime).TotalMilliseconds);
                    return rtt;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return -1;
            }
        }
    }
}
