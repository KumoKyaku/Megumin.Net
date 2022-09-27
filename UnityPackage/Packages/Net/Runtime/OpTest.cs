using Megumin.Remote;
using Megumin.Remote.Simple;
using System;
using System.Net;
using TMPro;
using UnityEngine;

public class OpTest : MonoBehaviour
{
    public TMP_InputField TargetIP;
    public TMP_InputField TargetPort;
    public TextMeshProUGUI Console;
    public TMP_InputField SendMessageText;

    // Start is called before the first frame update
    void Start()
    {
        Clear();
    }

    // Update is called once per frame
    void Update()
    {
        
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
        return new EchoTcp() {  };
    }

    public void Clear()
    {
        Console.text = "";
    }

    private EchoTcp client;
    public void ConnectTarget()
    {
        int port = 54321;
        int.TryParse(TargetPort.text, out port);
        IPAddress targetIP = IPAddress.Loopback;
        IPAddress.TryParse(TargetIP.text, out targetIP);

        client = new EchoTcp();

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
        Log($"发送：{send}");
        var resp = await client.Send<string>(SendMessageText.text);
        Log($"返回：{resp}");
    }
}
