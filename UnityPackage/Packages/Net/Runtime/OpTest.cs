using Megumin;
using Megumin.Message;
using Megumin.Remote;
using Megumin.Remote.Simple;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class OpTest : MonoBehaviour
{
    public TMP_InputField TargetIP;
    public TMP_InputField TargetPort;
    public TextMeshProUGUI Console;
    public TMP_InputField SendMessageText;
    public RTT RTT;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;
        Clear();
    }

    // Update is called once per frame
    void Update()
    {
        Megumin.Remote.MessageThreadTransducer.Update(Time.deltaTime);
        if (TimeLable)
        {
            TimeLable.text = DateTimeOffset.Now.ToString("HH:ss:fff");
        }
    }

    private TcpRemoteListener listener;
    private EchoTcp serverSide;


    public void Listen()
    {
        int port = 54321;
        int.TryParse(TargetPort.text, out port);
        listener?.Stop();
        listener = new TcpRemoteListener(port);
        Listen(listener);
        Log("��ʼ����");
    }

    private async void Listen(TcpRemoteListener remote)
    {
        /// ���һ�β��Ա���ͬʱ���пͻ��˷�����16000+����ʱ���������ܾ����ӡ�
        var accept = await remote.ListenAsync(Create);
        Listen(remote);
        if (accept != null)
        {
            Console.text += $"\n �յ����� {accept.Client.RemoteEndPoint} ";
            Log($"�յ����� {accept.Client.RemoteEndPoint}");
            serverSide = accept;
        }
    }

    public EchoTcp Create()
    {
        return new EchoTcp() {  };
    }

    public void Clear()
    {
        Console.text = "";
    }

    private Remote client;
    public void ConnectTarget()
    {
        int port = 54321;
        int.TryParse(TargetPort.text, out port);
        IPAddress targetIP = IPAddress.Loopback;
        IPAddress.TryParse(TargetIP.text, out targetIP);

        client = new Remote();
        client.Test = this;
        client.Post2ThreadScheduler = true;
        Connect(port, targetIP);
    }

    private async void Connect(int port, IPAddress targetIP)
    {
        try
        {
            Log($"��ʼ���� {targetIP} : {port}");
            await client.ConnectAsync(new IPEndPoint(targetIP, port));
            Console.text += $"\n ���ӳɹ�";
            Log($"���ӳɹ�");
            client.Logger = new MyLogger();
            RTT.SetTarget(client);
        }
        catch (Exception ex)
        {
            Log($"{ex.Message}");
        }
    }

    public void Log(string str)
    {
        Console.text += $"{str}\n";
    }

    int messageIndex = 0;
    public async void Send()
    {
        var send = string.Format(SendMessageText.text, messageIndex);
        messageIndex++;
        Log($"���ͣ�{send}");
        var resp = await client.SendSafeAwait<string>(send);
        Log($"���أ�{resp}");
    }

    [Button]
    public async void RemoteTime()
    {
        this.LogThreadID();
        var remotetime = await client.SendSafeAwait<DateTimeOffset>(new GetTime());
        var span = (DateTimeOffset.UtcNow - remotetime).TotalMilliseconds;
        Log($"Mytime:{DateTimeOffset.UtcNow}----RemoteTime:{remotetime}----offset:{(int)span}");
    }

    [Button]
    public async void RemoteTime2()
    {
        this.LogThreadID();
        SendOption sendOption = new SendOption()
        {
            RpcComplatePost2ThreadScheduler = false,
        };
        var remotetime = await client.SendSafeAwait<DateTimeOffset>(new GetTime(), options: sendOption).ConfigureAwait(false);
        var span = (DateTimeOffset.UtcNow - remotetime).TotalMilliseconds;
        await MainThread.Switch();
        Log($"Mytime:{DateTimeOffset.UtcNow}----RemoteTime:{remotetime}----offset:{(int)span}");
    }

    public async void TestRTT()
    {
        DateTimeOffset sendTime = DateTimeOffset.UtcNow;
        var (obj, ex) = await client.Send<Heartbeat>(Heartbeat.Default, options: SendOption.Echo);
        if (ex == null)
        {
            DateTimeOffset respTime = DateTimeOffset.UtcNow;
            var rtt = (int)((respTime - sendTime).TotalMilliseconds);
            Log($"RTT:{rtt}ms");
        }
        else
        {
            Log("RTT:--ms");
        }
    }

    [Button]
    public void ThreadTest()
    {
        this.LogThreadID(1);
        Task.Run(async () =>
        {
            this.LogThreadID(2);
            await Task.Delay(10);
            this.LogThreadID(3);
            await MainThread.Switch();
            this.LogThreadID(4);
        });
    }

    public TextMeshProUGUI TimeLable;
    public TextMeshProUGUI UtcNowOffset;
    public async void TimeStampSync()
    {
        List<Task<int>> tasks = new List<Task<int>>();
        for (int i = 0; i < 7; i++)
        {
            tasks.Add(GetOffset());
            await Task.Delay(100);
        }

        await Task.WhenAll(tasks);
        List<int> offsets = new List<int>();
        foreach (var item in tasks)
        {
            if (item.Result >= 0)
            {
                offsets.Add(item.Result);
            }
        }

        if (offsets.Count == 0)
        {
            return;
        }

        offsets.Sort();
        Debug.Log($"Offset Count:{offsets.Count} Min:{offsets.First()} Max:{offsets.Last()}");
        if (offsets.Count > 2)
        {
            offsets.RemoveAt(offsets.Count - 1);
            offsets.RemoveAt(0);
        }

        var offset = (int)offsets.Average();
        if (UtcNowOffset)
        {
            UtcNowOffset.text = offset.ToString();
        }
    }

    [Button]
    public async Task<int> GetOffset()
    {
        SendOption sendOption = new SendOption()
        {
            MillisecondsDelay = 2000,
            RpcComplatePost2ThreadScheduler = false,
        };
        var sendTime = DateTimeOffset.UtcNow;
        var (remotetime,ex) = await client.Send<DateTimeOffset>(new GetTime(), options: sendOption).ConfigureAwait(false);
        var offset = -1;
        if (ex == null)
        {
            var loacalUtcNow = DateTimeOffset.UtcNow;
            var rtt = loacalUtcNow - sendTime;
            var calRemoteUtcNow = remotetime + (rtt / 2);
            // LoacalUtcNow + Offset = RemoteUtcNow
            var offsetSpan = calRemoteUtcNow - loacalUtcNow;
            offset = (int)offsetSpan.TotalMilliseconds;
        }
        Debug.Log($"����UtcNow offset:{offset}");
        return offset;
    }

    public class Remote : TcpRemote
    {
        public OpTest Test { get; internal set; }

        protected override async ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Test.Log($"���գ�{message}");
            return base.OnReceive(cmd, messageID, message);
        }

        public override void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            base.OnSendSafeAwaitException(request, response, onException, finnalException);
            Debug.Log(finnalException);
            Test.Log($"���գ�{finnalException}");
        }
    }

    internal class MyLogger : IMeguminRemoteLogger
    {
        public void Log(string error)
        {
            Debug.LogError(error);
        }
    }
}

