using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Ping = System.Net.NetworkInformation.Ping;
using System.Threading;
using System.Security.Cryptography;

namespace Megumin
{
    public class PingTool : MonoBehaviour
    {
        public TMP_InputField TargetIP;
        public TextMeshProUGUI RoundtripTime;
        public TextMeshProUGUI PingResult;
        private CancellationTokenSource cancellationTokenSource;

        public async void Ping()
        {
            string ip = TargetIP.text;
            await Ping(ip);
        }

        public async ValueTask Ping(string ip)
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            if (IPAddress.TryParse(ip, out var ipAddress))
            {
                Ping ping = new Ping();
                var res = await ping.SendPingAsync(ipAddress);
                Debug.Log($"Ping {TargetIP.text} {res.Status} {res.RoundtripTime}ms");
                PingResult.text = res.Status.ToString();
                RoundtripTime.SetText("{0}ms", res.RoundtripTime);
            }
#else
            UnityEngine.Ping ping = new UnityEngine.Ping(ip);
            PingResult.text = "Ping...";
            while (!ping.isDone)
            {
                await Task.Delay(10);
            }
            PingResult.text = "Success";
            RoundtripTime.text = $"{ping.time}ms";
#endif
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
        }

        public void LoopPing()
        {
            string ip = "";
            if (TargetIP)
            {
                ip = TargetIP.text;
            }
            LoopPing(ip);
        }

        public void LoopPing(string ip)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            LoopPing(ip, cancellationTokenSource.Token);
        }

        public async void LoopPing(string ip, CancellationToken token)
        {
            await Ping(ip);
            await Task.Delay(500);
            if (!token.IsCancellationRequested)
            {
                LoopPing(ip, token);
            }
        }
    }
}




