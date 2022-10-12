using Megumin.Remote;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Megumin.Message;
using Net.Remote;

namespace Megumin
{
    /// <summary>
    /// RTT不等于ping和延迟，具体参见文档
    /// </summary>
    public class RTT : MonoBehaviour
    {
        public TextMeshProUGUI RTTValue;
        public int IntervalMilliseconds = 500;
        private ISendCanAwaitable Target;
        private CancellationTokenSource cancellation;

        internal void SetTarget(ISendCanAwaitable client)
        {
            this.Target = client;
            cancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            TestRTT(cancellation.Token);
        }

        private async void TestRTT(CancellationToken token)
        {
            await Task.Delay(IntervalMilliseconds);
            if (Target != null)
            {
                DateTimeOffset sendTime = DateTimeOffset.UtcNow;
                var (obj, ex) = await Target.Send<Heartbeat>(Heartbeat.Default, options: SendOption.Echo);
                if (ex == null)
                {
                    DateTimeOffset respTime = DateTimeOffset.UtcNow;
                    var rtt = (int)((respTime - sendTime).TotalMilliseconds);
                    if (RTTValue)
                    {
                        RTTValue.SetText("RTT:{0}ms", rtt);
                    }

                    if (!token.IsCancellationRequested)
                    {
                        TestRTT(token);
                    }
                }
                else
                {
                    RTTValue.SetText("RTT:--ms");
                }
            }
            else
            {
                RTTValue.SetText("RTT:--ms");
            }
        }

        private void Reset()
        {
            if (!RTTValue)
            {
                RTTValue = GetComponentInChildren<TextMeshProUGUI>();
            }
        }
    }
}

