using Megumin;
using Megumin.Message;
using Megumin.Remote;
using Megumin.Remote.Simple;
using Net.Remote;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

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
            TimeLable.text = DateTimeOffset.Now.ToString("HH:mm:ss:fff");
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
        Log("开始监听");
    }

    private async void Listen(TcpRemoteListener remote)
    {
        /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
        var accept = await remote.ListenAsync(Create);
        Listen(remote);
        if (accept != null)
        {
            Console.text += $"\n 收到连接 {accept.Client.RemoteEndPoint} ";
            Log($"收到连接 {accept.Client.RemoteEndPoint}");
            serverSide = accept;
        }
    }

    public EchoTcp Create()
    {
        return new EchoTcp() { };
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
            Log($"开始连接 {targetIP} : {port}");
            await client.ConnectAsync(new IPEndPoint(targetIP, port));
            Console.text += $"\n 连接成功";
            Log($"连接成功");
            client.Logger = new MyLogger();
            RTT.SetTarget(client);
            Auth();
        }
        catch (Exception ex)
        {
            Log($"{ex.Message}");
        }
    }

    public void Disconnect()
    {
        client.Disconnect();
    }

    public async ValueTask Auth()
    {
        Authentication authentication = new Authentication();
        authentication.Token = "TestClient9527";
        var (result,ex) = await client.Send<int>(authentication);
        if (ex == null)
        {
            if (result == 200)
            {
                Log($"{authentication.Token} 认证成功");
            }
            else
            {
                Log($"{authentication.Token} 认证失败 {result}");
            }
        }
        else
        {
            Log($"{authentication.Token} 认证失败 {ex}");
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
        Log($"发送：{send}");
        var resp = await client.SendSafeAwait<string>(send);
        Log($"返回：{resp}");
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
    public int TimeStampSyncSampleCount = 7;
    public int TimeStampSyncSampleInterval = 100;
    public async void TimeStampSync()
    {
        TimeStampSynchronization synchronization = new TimeStampSynchronization();
        synchronization.DebugLog = true;
        await synchronization.Sync(client, TimeStampSyncSampleCount, TimeStampSyncSampleInterval);
        if (UtcNowOffset)
        {
            UtcNowOffset.text = synchronization.OffsetMilliseconds.ToString();
        }
    }

    public class TimeStampSynchronization : Megumin.Remote.TimeStampSynchronization
    {
        public override void Log(object obj)
        {
            Debug.Log(obj);
        }
    }

    public class Remote : TcpRemote
    {
        public OpTest Test { get; internal set; }

        protected override async ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Test.Log($"接收：{message}");
            return base.OnReceive(cmd, messageID, message);
        }

        public override void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            base.OnSendSafeAwaitException(request, response, onException, finnalException);
            Debug.Log(finnalException);
            Test.Log($"接收：{finnalException}");
        }

        protected override void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {
            Test.OnDisconnect(error, activeOrPassive);
        }

        internal void ReConnectFrom(Remote client)
        {
            //SendPipe = client.SendPipe;
            RpcLayer = client.RpcLayer;
        }
    }

    public GameObject AutoReConnectUI;
    private void OnDisconnect(SocketError error, ActiveOrPassive activeOrPassive)
    {
        CancellationTokenSource source = new CancellationTokenSource();
        ReConnect(source.Token);
    }

    public GameObject AutoReConnectErrorUI;
    public void ReConnectError()
    {
        if (AutoReConnectUI)
        {
            AutoReConnectUI.SetActive(false);
        }

        if (AutoReConnectErrorUI)
        {
            AutoReConnectErrorUI.SetActive(true);
        }
    }

    public async void ReConnect(CancellationToken token)
    {
        if (AutoReConnectUI)
        {
            AutoReConnectUI.SetActive(true);
        }

        client.Disconnect();

        var  client2 = new Remote();
        client2.Test = this;
        client2.Post2ThreadScheduler = true;

        try
        {
            Log($"断线重连 {client.ConnectIPEndPoint.Address} : {client.ConnectIPEndPoint.Port}");
            await client2.ConnectAsync(client.ConnectIPEndPoint);
            if (token.IsCancellationRequested)
            {
                client2.Disconnect();
            }
            else
            {
                Console.text += $"\n 连接成功";
                Log($"连接成功");
                client.Logger = new MyLogger();
                RTT.SetTarget(client);
                await Auth();
                if (token.IsCancellationRequested)
                {
                    client2.Disconnect();
                }

                //将旧发送缓存 连接到新 remote
                client2.ReConnectFrom(client);
            }
        }
        catch (Exception ex)
        {
            Log($"{ex.Message}");
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

