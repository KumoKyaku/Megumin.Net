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

public class PingTest : MonoBehaviour
{
    public TMP_InputField TargetIP;
    public TextMeshProUGUI PingResult;
    public TextMeshProUGUI RoundtripTime;
    private CancellationTokenSource cancellationTokenSource;

    // Start is called before the first frame update
    void Start()
    {

    }

    public async void Ping()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        await UnityPingTargetIP();
#else
        Ping ping = new Ping();
        await PingTargetIP(ping);
#endif
    }

    async Task PingTargetIP(Ping ping)
    {
        if (IPAddress.TryParse(TargetIP.text, out var iP))
        {
            var res = await ping.SendPingAsync(iP);
            Debug.Log($"Ping {TargetIP.text} {res.Status} {res.RoundtripTime}ms");
            PingResult.text = res.Status.ToString();
            RoundtripTime.text = $"{res.RoundtripTime}ms";
        }
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
    }

    public void PingLoop()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

#if UNITY_ANDROID && !UNITY_EDITOR
        UnityLoop(cancellationTokenSource.Token);
#else
        Ping ping = new Ping();
        Loop(ping, cancellationTokenSource.Token);
#endif
    }

    private async void Loop(Ping ping, CancellationToken token)
    {
        await PingTargetIP(ping);
        await Task.Delay(500);
        if (!token.IsCancellationRequested)
        {
            Loop(ping, token);
        }
    }

    public async Task UnityPingTargetIP()
    {
        UnityEngine.Ping ping = new UnityEngine.Ping(TargetIP.text);
        PingResult.text = "Ping...";
        while (!ping.isDone)
        {
            await Task.Delay(10);
        }
        PingResult.text = "Success";
        RoundtripTime.text = $"{ping.time}ms";
    }

    private async void UnityLoop(CancellationToken token)
    {
        await UnityPingTargetIP();
        await Task.Delay(500);
        if (!token.IsCancellationRequested)
        {
            UnityLoop(token);
        }
    }
}
