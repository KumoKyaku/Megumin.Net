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
        client.Logger = new MyLogger();
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
            Log($"\n 连接成功");
            await Auth(client);

            RTT.SetTarget(client);
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

    /// <summary>
    /// 认证需要手动指定remote，断线重连时也要用。
    /// </summary>
    /// <param name="remote"></param>
    /// <returns></returns>
    public async ValueTask<bool> Auth(ISendCanAwaitable remote)
    {
        Authentication authentication = new Authentication();
        authentication.Token = "TestClient9527";
        var (result,ex) = await remote.Send<int>(authentication);
        if (ex == null)
        {
            if (result == 200)
            {
                Log($"{authentication.Token} 认证成功");
                return true;
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
        return false;
    }

    public async void Log(string str)
    {
        await MainThread.Switch();
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
            client.SendPipe.CancelPendingRead();
            SendPipe.CancelPendingRead();
            SendPipe = client.SendPipe;
            RpcLayer = client.RpcLayer;
            StartWork();
        }
    }

    public GameObject AutoReConnectUI;
    private async void OnDisconnect(SocketError error, ActiveOrPassive activeOrPassive)
    {
        await MainThread.Switch();
        Log($"\n OnDisconnect {error} : {activeOrPassive}");
        ReConnect();
    }

    public void CancelReconnect()
    {
        reconnectCancelSource?.Cancel();
    }

    public void RetryReconnect()
    {
        reconnectCancelSource?.Cancel();
        ReConnect();
    }

    public void Home()
    {

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


    CancellationTokenSource reconnectCancelSource = null;
    /// <summary>
    /// 重来三种方式：
    /// 1: 新创建一个Socket，设置到旧的remote中。
    /// 2：新建一个remote，并使用旧的remote的rpclayer，sendpipe。
    /// 3：新建一个remote，旧的remote使用 新remote的socket revepipe,重新激活旧的remote
    /// 只有方法2成立。重连后需要验证发消息。需要使用remote功能，所以1不成立。
    /// 收发消息后，socket ReceiveAsync已经挂起，socket已经和remote绑定，不能切换socket，所以方法3不成立。
    /// </summary>
    public async void ReConnect()
    {
        reconnectCancelSource?.Cancel();
        reconnectCancelSource = new CancellationTokenSource();
        var token = reconnectCancelSource.Token;

        if (AutoReConnectUI)
        {
            AutoReConnectUI.SetActive(true);
        }

        if (AutoReConnectErrorUI)
        {
            AutoReConnectErrorUI.SetActive(false);
        }

        await Task.Delay(1000);
        client.Disconnect();

        var  client2 = new Remote();
        client2.Logger = new MyLogger();
        client2.Test = this;
        client2.Post2ThreadScheduler = true;

        try
        {
            Log($"\n 断线重连 {client.ConnectIPEndPoint.Address} : {client.ConnectIPEndPoint.Port}");
            await client2.ConnectAsync(client.ConnectIPEndPoint);
            if (!token.IsCancellationRequested)
            {
                Log($"\n 连接成功");
                var success = await Auth(client2);
                RTT.SetTarget(client2);
                if (success && !token.IsCancellationRequested)
                {
                    //使用旧的remote的rpclayer，sendpipe。
                    client2.ReConnectFrom(client);
                    client = client2;
                    //重连成功
                    if (AutoReConnectUI)
                    {
                        AutoReConnectUI.SetActive(false);
                    }
                    return;
                }
            }
           
            //重连失败
            client2.Disconnect();
            ReConnectError();
        }
        catch (Exception ex)
        {
            Log($"\n {ex.Message}");
            ReConnectError();
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

