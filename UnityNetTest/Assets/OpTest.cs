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
    // Start is called before the first frame update
    void Start()
    {
        
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
        Console.text += $"\n ��ʼ����";
    }

    private async void Listen(TcpRemoteListener remote)
    {
        /// ���һ�β��Ա���ͬʱ���пͻ��˷�����16000+����ʱ���������ܾ����ӡ�
        var accept = await remote.ListenAsync(Create);
        Listen(remote);
        if (accept != null)
        {
            Console.text += $"\n �յ����� {accept.Client.RemoteEndPoint} ";
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
            Console.text += $"\n ��ʼ���� {targetIP} : {port}";
            await client.ConnectAsync(new IPEndPoint(targetIP, port));
            Console.text += $"\n ���ӳɹ�";
        }
        catch (Exception ex)
        {
            Console.text += $"\n {ex.Message}";
        }
    }
}
