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

public class PingTest : MonoBehaviour
{
    public TMP_InputField TargetIP;
    public TextMeshProUGUI PingResult;
    private CancellationTokenSource cancellationTokenSource;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public async void Ping()
    {
        Ping ping = new Ping();
        await PingTargetIP(ping);
    }

    async Task PingTargetIP(Ping ping)
    {
        if (IPAddress.TryParse(TargetIP.text, out var iP))
        {
            var res = await ping.SendPingAsync(iP);
            Debug.Log($"Ping {TargetIP.text} {res.Status} {res.RoundtripTime}ms");
            if (res.Status == IPStatus.Success)
            {
                PingResult.text = $"{res.RoundtripTime}ms";
            }
            else
            {
                PingResult.text = res.Status.ToString();
            }
        }
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
    }

    public void PingLoop()
    {
        Ping ping = new Ping();
        cancellationTokenSource = new CancellationTokenSource();
        Loop(ping, cancellationTokenSource.Token);
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
}
