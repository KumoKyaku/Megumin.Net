using Megumin;
using Megumin.Message;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Net;
using System.Net.Sockets;
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
            TimeLable.text = DateTimeOffset.Now.ToString("HH:mm:ss:fff");
        }
    }

    public Protocol ProtocolType = Protocol.Tcp;

    public void OnProtocolTypeChange(int index)
    {
        switch (index)
        {
            case 0:
                ProtocolType = Protocol.Tcp;
                break;
            case 1:
                ProtocolType = Protocol.Udp;
                break;
            case 2:
                ProtocolType = Protocol.Kcp;
                break;
            default:
                break;
        }
    }

    private IListener listener;
    private IRemote serverSide;


    public async void Listen()
    {
        int port = 54321;
        int.TryParse(TargetPort.text, out port);
        switch (ProtocolType)
        {
            case Protocol.Tcp:
                {
                    listener?.Stop();
                    var rl = new TcpRemoteListener(port);
                    Log($"开始监听 {ProtocolType}");
                    while (true)
                    {
                        var accept = await rl.ReadAsync(() =>
                        {
                            return new TcpRemote();
                        });

                        if (accept != null)
                        {
                            Console.text += $"\n 收到连接 {accept.Client.RemoteEndPoint} ";
                            Log($"收到连接 {accept.Client.RemoteEndPoint}");
                            serverSide = accept;
                        }
                    }
                }
                break;
            case Protocol.Udp:
                {
                    listener?.Stop();
                    var rl = new UdpRemoteListener(port, AddressFamily.InterNetwork);
                    Log($"开始监听 {ProtocolType}");
                    while (true)
                    {
                        var accept = await rl.ReadAsync(() =>
                        {
                            return new TestUdpRemote(AddressFamily.InterNetwork);
                        });

                        if (accept != null)
                        {
                            Console.text += $"\n 收到连接 {accept.Client.RemoteEndPoint} ";
                            Log($"收到连接 {accept.Client.RemoteEndPoint}");
                            serverSide = accept;
                        }
                    }
                }
                break;
            case Protocol.Kcp:
                {
                    listener?.Stop();
                    var rl = new KcpRemoteListener(port);
                    Log($"开始监听 {ProtocolType}");
                    while (true)
                    {
                        var accept = await rl.ReadAsync(() =>
                        {
                            return new TestKcpRemote(AddressFamily.InterNetwork);
                        });

                        if (accept != null)
                        {
                            Console.text += $"\n 收到连接 {accept.Client.RemoteEndPoint} ";
                            Log($"收到连接 {accept.Client.RemoteEndPoint}");
                            serverSide = accept;
                        }
                    }
                }
                break;
            default:
                break;
        }

    }

    public void Clear()
    {
        Console.text = "";
    }

    private IRemote client;
    public void ConnectTarget()
    {
        int port = 54321;
        int.TryParse(TargetPort.text, out port);
        IPAddress targetIP = IPAddress.Loopback;
        IPAddress.TryParse(TargetIP.text, out targetIP);

        if (ProtocolType == Protocol.Tcp)
        {
            var testClient = new TestTcpRemote();
            testClient.TraceListener = new UnityTraceListener();
            testClient.Test = this;
            testClient.Post2ThreadScheduler = true;
            client = testClient;
            Connect(testClient, port, targetIP);
        }
        else if (ProtocolType == Protocol.Udp)
        {
            var testClient = new TestUdpRemote(AddressFamily.InterNetwork);
            testClient.TraceListener = new UnityTraceListener();
            testClient.Test = this;
            testClient.Post2ThreadScheduler = true;
            client = testClient;
            Connect(testClient, port, targetIP);
        }
        else if (ProtocolType == Protocol.Kcp)
        {
            var testClient = new TestKcpRemote(AddressFamily.InterNetwork);
            testClient.TraceListener = new UnityTraceListener();
            testClient.Test = this;
            testClient.Post2ThreadScheduler = true;
            client = testClient;
            Connect(testClient, port, targetIP);
        }
    }

    private async void Connect<T>(T client, int port, IPAddress targetIP)
        where T : IConnectable, ISendCanAwaitable
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
        if (client is IConnectable connectable)
        {
            connectable.Disconnect();
        }
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
        var (result, ex) = await remote.Send<int>(authentication);
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
        Interlocked.Increment(ref messageIndex);
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
        if (AutoReConnectUI)
        {
            AutoReConnectUI.SetActive(false);
        }

        if (AutoReConnectErrorUI)
        {
            AutoReConnectErrorUI.SetActive(false);
        }
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

        //模拟重连过程，正式代码注释掉
        await Task.Delay(1000);

        if (client is TestTcpRemote oldTcpRemote)
        {
            oldTcpRemote.Disconnect();

            var client2 = new TestTcpRemote();
            client2.TraceListener = new UnityTraceListener();
            client2.Test = this;
            client2.Post2ThreadScheduler = true;

            try
            {
                Log($"\n 断线重连 {oldTcpRemote.ConnectIPEndPoint.Address} : {oldTcpRemote.ConnectIPEndPoint.Port}");
                //在两秒内重连
                var res = await client2.ConnectAsync(oldTcpRemote.ConnectIPEndPoint).WaitAsync(2000);
                if (res && !token.IsCancellationRequested)
                {
                    Log($"\n 连接成功");
                    var success = await Auth(client2);

                    if (success && !token.IsCancellationRequested)
                    {
                        //使用旧的remote的rpclayer，sendpipe。
                        client2.ReConnectFrom(oldTcpRemote);
                        oldTcpRemote = client2;

                        RTT.SetTarget(oldTcpRemote);
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
                ReConnectError();
                Log($"\n {ex.Message}");
                //重连失败
                client2.Disconnect();
            }
        }

    }

    public class TestTcpRemote : TcpRemote
    {
        public OpTest Test { get; internal set; }

        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Test.Log($"接收：{message}");
            return new ValueTask<object>(base.OnReceive(cmd, messageID, message));
        }

        public override void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            base.OnSendSafeAwaitException(request, response, onException, finnalException);
            Debug.Log(finnalException);
            Test.Log($"接收：{finnalException}");
        }

        protected override async void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {
#if UNITY_EDITOR
            await MainThread.Switch();
            if (UnityEditor.EditorApplication.isPlaying == false)
            {
                //防止编辑器停止播放时触发断线重连
                return;
            }
#endif
            Test.OnDisconnect(error, activeOrPassive);
        }
    }

    public class TestUdpRemote : UdpRemote
    {
        public TestUdpRemote(AddressFamily? addressFamily) : base(addressFamily)
        {

        }

        public OpTest Test { get; internal set; }

        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Test.Log($"接收：{message}");
            return new ValueTask<object>(base.OnReceive(cmd, messageID, message));
        }

        public override void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            base.OnSendSafeAwaitException(request, response, onException, finnalException);
            Debug.Log(finnalException);
            Test.Log($"接收：{finnalException}");
        }

        protected override async void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {
#if UNITY_EDITOR
            await MainThread.Switch();
            if (UnityEditor.EditorApplication.isPlaying == false)
            {
                //防止编辑器停止播放时触发断线重连
                return;
            }
#endif
            Test.OnDisconnect(error, activeOrPassive);
        }
    }

    public class TestKcpRemote : KcpRemote
    {
        public TestKcpRemote(AddressFamily? addressFamily) : base(addressFamily)
        {

        }

        public OpTest Test { get; internal set; }

        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Test.Log($"接收：{message}");
            return new ValueTask<object>(base.OnReceive(cmd, messageID, message));
        }

        public override void OnSendSafeAwaitException(object request, object response, Action<Exception> onException, Exception finnalException)
        {
            base.OnSendSafeAwaitException(request, response, onException, finnalException);
            Debug.Log(finnalException);
            Test.Log($"接收：{finnalException}");
        }

        protected override async void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
        {
#if UNITY_EDITOR
            await MainThread.Switch();
            if (UnityEditor.EditorApplication.isPlaying == false)
            {
                //防止编辑器停止播放时触发断线重连
                return;
            }
#endif
            Test.OnDisconnect(error, activeOrPassive);
        }
    }
}

