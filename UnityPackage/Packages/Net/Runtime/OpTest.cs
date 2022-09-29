using Megumin;
using Megumin.Message;
using Megumin.Remote;
using Megumin.Remote.Simple;
using System;
using System.Net;
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

    public async void RemoteTime()
    {
        var remotetime = await client.SendSafeAwait<DateTimeOffset>(new GetTime());
        var span = (DateTimeOffset.UtcNow - remotetime).TotalMilliseconds;
        Log($"Mytime:{DateTimeOffset.UtcNow}----RemoteTime:{remotetime}----offset:{(int)span}");
    }

    public class Remote : TcpRemote
    {
        public OpTest Test { get; internal set; }

        protected override async ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            Test.Log($"���գ�{message}");
            return base.OnReceive(cmd, messageID, message);
        }
    }
}
