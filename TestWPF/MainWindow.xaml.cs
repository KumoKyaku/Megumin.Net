using Megumin.Message;
using Megumin.Remote;
using Megumin.Remote.Simple;
using Megumin.Remote.Test;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TestWPF;
using System.Collections.Generic;
using Net.Remote;
using Megumin.Remote;

namespace TestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ITestRemote client;
        private ITestRemote server;
        private TcpRemoteListener listener;
        private UdpRemoteListener UdpRemoteListener;
        private KcpRemoteListener KcpRemoteListener;

        static Protocol ProtocolType = Megumin.Remote.Protocol.Tcp;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ProtocolType == Megumin.Remote.Protocol.Tcp)
            {
                ListenTcp();
            }
            else if (ProtocolType == Megumin.Remote.Protocol.Udp)
            {
                ListenUdp();
            }
            else
            {
                ListenKcp();
            }
            this.Serverlog.Content += $"\n 开始监听 {ProtocolType}";
        }

        async void ListenTcp()
        {
            int port = 54321;
            int.TryParse(ListenPort.Text, out port);
            listener = new TcpRemoteListener(port);
            listener.Start();

            while (true)
            {
                var accept = await listener.ReadAsync(() =>
                {
                    var r = new TestRemote() { log = this.Serverlog };
                    return r;
                });

                if (accept != null)
                {
                    accept.LogRecvBytes = this.LogRecvBytes.IsChecked ?? false;
                    this.Serverlog.Content += $"\n 收到连接 {accept.RemoteEndPoint} ";
                    server = accept;
                }
            }
        }

        async void ListenUdp()
        {
            int port = 54321;
            int.TryParse(ListenPort.Text, out port);
            UdpRemoteListener = new UdpRemoteListener(port);
            UdpRemoteListener.Start();

            while (true)
            {
                var accept = await UdpRemoteListener.ReadAsync(() =>
                {
                    var r = new TestUdpRemote() { log = this.Serverlog };
                    return r;
                });

                if (accept != null)
                {
                    accept.LogRecvBytes = this.LogRecvBytes.IsChecked ?? false;
                    this.Serverlog.Dispatcher.Invoke(new Action(() =>
                    {
                        this.Serverlog.Content += $"\n 收到连接 {accept.RemoteEndPoint} ";
                    }));
                    server = accept;
                }
            }
        }

        async void ListenKcp()
        {
            int port = 54321;
            int.TryParse(ListenPort.Text, out port);
            KcpRemoteListener = new KcpRemoteListener(port);
            KcpRemoteListener.Start();

            while (true)
            {
                var accept = await KcpRemoteListener.ReadAsync(() =>
                {
                    var r = new TestKcpRemote() { log = this.Serverlog };
                    return r;
                }).ConfigureAwait(true);

                if (accept != null)
                {
                    accept.LogRecvBytes = this.LogRecvBytes.IsChecked ?? false;
                    this.Serverlog.Dispatcher.Invoke(new Action(() =>
                    {
                        this.Serverlog.Content += $"\n 收到连接 {accept.RemoteEndPoint} ";
                    }));
                    server = accept;
                }
            }
        }

        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
            listener?.Stop();
            UdpRemoteListener?.Stop();
            KcpRemoteListener?.Stop();
        }

        private void CreateClient(object sender, RoutedEventArgs e)
        {
            int port = 54321;
            int.TryParse(ConnectPort.Text, out port);
            IPAddress targetIP = IPAddress.Loopback;
            IPAddress.TryParse(TargetIP.Text, out targetIP);

            if (ProtocolType == Megumin.Remote.Protocol.Tcp)
            {
                client = new TestRemote();
            }
            else if (ProtocolType == Megumin.Remote.Protocol.Udp)
            {
                client = new TestUdpRemote();
            }
            else if (ProtocolType == Megumin.Remote.Protocol.Kcp)
            {
                client = new TestKcpRemote();
            }

            client.log = this.ClientLog;

            Connect(port, targetIP);
        }

        private async void Connect(int port, IPAddress targetIP)
        {
            try
            {
                await client.ConnectAsync(new IPEndPoint(targetIP, port));
                ClientLog.Content += $"\n 连接成功";
            }
            catch (Exception ex)
            {
                ClientLog.Content += $"\n {ex.Message}";
            }
        }

        private void ServerSendMSG(object sender, RoutedEventArgs e)
        {
            server.Send(new TestPacket1() { Value = 200 });
        }

        private async void TestRpc(object sender, RoutedEventArgs e)
        {
            var resp = await client.SendSafeAwait<TestPacket2>(new TestPacket2() { Value = 999 });
            if (resp.Value == 999)
            {
                ClientLog.Content += $"\n Rpc测试成功";
            }
            else
            {
                ClientLog.Content += $"\n Rpc测试失败";
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            client.Client.Shutdown(SocketShutdown.Send);
        }

        private void ServerDisconnect(object sender, RoutedEventArgs e)
        {
            server.Disconnect();
        }

        private void ClientDisconnect(object sender, RoutedEventArgs e)
        {
            client.Disconnect(true);
        }

        private void ClientSend(object sender, RoutedEventArgs e)
        {
            client.Send(new TestPacket1() { Value = 100 }); ;
        }

        private void ClearLog(object sender, RoutedEventArgs e)
        {
            ClientLog.Content = "";
            Serverlog.Content = "";
        }

        private async void RPCString_Click(object sender, RoutedEventArgs e)
        {
            const string TestStr = "RPCString测试";
            var resp = await client.SendSafeAwait<string>(TestStr);
            if (resp == TestStr)
            {
                ClientLog.Content += $"\n RPCString测试成功";
            }
            else
            {
                ClientLog.Content += $"\n RPCString测试失败";
            }
        }

        private void SendString_Click(object sender, RoutedEventArgs e)
        {
            server.Send($"测试String {DateTimeOffset.Now}");
        }

        private void SendBigMessage_Click(object sender, RoutedEventArgs e)
        {
            client.Send(new TestPacket3() { Value = 300 }); ;
        }

        private async void TestTime_Click(object sender, RoutedEventArgs e)
        {
            var remotetime = await client.SendSafeAwait<DateTimeOffset>(new GetTime());
            var span = (DateTimeOffset.UtcNow - remotetime).TotalMilliseconds;
            ClientLog.Content += $"\n Mytime:{DateTimeOffset.UtcNow}----RemoteTime:{remotetime}----offset:{(int)span}";
        }

        private void LogRecvBytes_Checked(object sender, RoutedEventArgs e)
        {
            if (server != null)
            {
                server.LogRecvBytes = true;
            }

        }

        private void LogRecvBytes_Unchecked(object sender, RoutedEventArgs e)
        {
            if (server != null)
            {
                server.LogRecvBytes = false;
            }
        }

        private void StartSocketSend_Click(object sender, RoutedEventArgs e)
        {
            client.StartSocketSend();
        }

        private void StopSocketSend_Click(object sender, RoutedEventArgs e)
        {
            client.StopSocketSend();
        }

        private void Protocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (Protocol.SelectedIndex)
            {
                case 0:
                    ProtocolType = Megumin.Remote.Protocol.Tcp;
                    break;
                case 1:
                    ProtocolType = Megumin.Remote.Protocol.Udp;
                    break;
                case 2:
                    ProtocolType = Megumin.Remote.Protocol.Kcp;
                    break;
                default:
                    break;
            }
        }
    }
}

public interface ITestRemote : IRemote, IConnectable, ISocketSendable
{
    Label log { get; set; }
    bool LogRecvBytes { get; set; }
}

public class TestRemote : TcpRemote, ITestRemote
{
    static Dictionary<string, TestRemote> AllClient = new Dictionary<string, TestRemote>();
    public Label log { get; set; }
    public bool LogRecvBytes { get; set; }

    int disCount = 0;
    protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
    {
        log.Dispatcher.Invoke(() =>
        {
            switch (message)
            {
                case TestPacket1 packet1:
                    log.Content += $"\n 收到{nameof(TestPacket1)} value:{packet1.Value}";
                    break;
                case TestPacket2 packet2:
                    log.Content += $"\n 收到{nameof(TestPacket2)} value:{packet2.Value}";
                    break;
                case TestPacket3 packet3:
                    log.Content += $"\n 收到{nameof(TestPacket3)} value:{packet3.Value}";
                    break;
                case Authentication auth:
                    log.Content += $"\n 收到{nameof(Authentication)} Token:{auth.Token}";
                    break;
                default:
                    log.Content += $"\n 收到{message.GetType().Name} value:{message} {RemoteEndPoint}";
                    break;
            }
        });

        switch (message)
        {
            case TestPacket2 packet2:
                return new ValueTask<object>(message);
            case Authentication auth2:
                if (AllClient.ContainsKey(auth2.Token))
                {
                    return new ValueTask<object>(200);
                }
                if (auth2.Token.StartsWith("TestClient"))
                {
                    AllClient.Add(auth2.Token, this);
                    return new ValueTask<object>(200);
                }
                else
                {
                    return new ValueTask<object>(404);
                }
            case string str:
                return new ValueTask<object>(str);
            default:
                break;
        }
        return NullResult;
    }

    protected override void ProcessBody(in ReadOnlySequence<byte> bodyBytes, int RpcID, short CMD, int MessageID, object options)
    {
        if (LogRecvBytes)
        {
            var len = bodyBytes.Length;
            log.Dispatcher.Invoke(() =>
            {
                log.Content += $"\n 收到bodyBytes len:{len} rpcID：{RpcID} CMD:{CMD}  MessageID:{MessageID}";
            });
        }

        base.ProcessBody(bodyBytes, RpcID, CMD, MessageID, options);
    }

    protected override void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
    {
        disCount++;
        log.Dispatcher.Invoke(() =>
        {
            log.Content += $"\n 网络已断开。调用次数{disCount} \n {error} -- {activeOrPassive}";
        });
    }
}

public class TestUdpRemote : UdpRemote, ITestRemote
{
    static Dictionary<string, TestUdpRemote> AllClient = new Dictionary<string, TestUdpRemote>();
    public Label log { get; set; }
    public bool LogRecvBytes { get; set; }

    int disCount = 0;
    protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
    {
        log.Dispatcher.Invoke(() =>
        {
            switch (message)
            {
                case TestPacket1 packet1:
                    log.Content += $"\n 收到{nameof(TestPacket1)} value:{packet1.Value}";
                    break;
                case TestPacket2 packet2:
                    log.Content += $"\n 收到{nameof(TestPacket2)} value:{packet2.Value}";
                    break;
                case TestPacket3 packet3:
                    log.Content += $"\n 收到{nameof(TestPacket3)} value:{packet3.Value}";
                    break;
                case Authentication auth:
                    log.Content += $"\n 收到{nameof(Authentication)} Token:{auth.Token}";
                    break;
                default:
                    log.Content += $"\n 收到{message.GetType().Name} value:{message} {RemoteEndPoint}";
                    break;
            }
        });

        switch (message)
        {
            case TestPacket2 packet2:
                return new ValueTask<object>(message);
            case Authentication auth2:
                if (AllClient.ContainsKey(auth2.Token))
                {
                    return new ValueTask<object>(200);
                }
                if (auth2.Token.StartsWith("TestClient"))
                {
                    AllClient.Add(auth2.Token, this);
                    return new ValueTask<object>(200);
                }
                else
                {
                    return new ValueTask<object>(404);
                }
            case string str:
                return new ValueTask<object>(str);
            default:
                break;
        }
        return NullResult;
    }

    protected override void ProcessBody(in ReadOnlySequence<byte> bodyBytes, int RpcID, short CMD, int MessageID, object options)
    {
        if (LogRecvBytes)
        {
            var len = bodyBytes.Length;
            log.Dispatcher.Invoke(() =>
            {
                log.Content += $"\n 收到bodyBytes len:{len} rpcID：{RpcID} CMD:{CMD}  MessageID:{MessageID}";
            });
        }

        base.ProcessBody(bodyBytes, RpcID, CMD, MessageID, options);
    }

    protected override void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
    {
        disCount++;
        log.Dispatcher.Invoke(() =>
        {
            log.Content += $"\n 网络已断开。调用次数{disCount} \n {error} -- {activeOrPassive}";
        });
    }

    public bool IsSocketSending { get; }

    public void StartSocketSend()
    {
        throw new NotImplementedException();
    }

    public void StopSocketSend()
    {
        throw new NotImplementedException();
    }
}

public class TestKcpRemote : KcpRemote, ITestRemote
{
    static Dictionary<string, TestKcpRemote> AllClient = new Dictionary<string, TestKcpRemote>();
    public Label log { get; set; }
    public bool LogRecvBytes { get; set; }

    int disCount = 0;
    protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
    {
        log.Dispatcher.Invoke(() =>
        {
            switch (message)
            {
                case TestPacket1 packet1:
                    log.Content += $"\n 收到{nameof(TestPacket1)} value:{packet1.Value}";
                    break;
                case TestPacket2 packet2:
                    log.Content += $"\n 收到{nameof(TestPacket2)} value:{packet2.Value}";
                    break;
                case TestPacket3 packet3:
                    log.Content += $"\n 收到{nameof(TestPacket3)} value:{packet3.Value}";
                    break;
                case Authentication auth:
                    log.Content += $"\n 收到{nameof(Authentication)} Token:{auth.Token}";
                    break;
                default:
                    log.Content += $"\n 收到{message.GetType().Name} value:{message} {RemoteEndPoint}";
                    break;
            }
        });

        switch (message)
        {
            case TestPacket2 packet2:
                return new ValueTask<object>(message);
            case Authentication auth2:
                if (AllClient.ContainsKey(auth2.Token))
                {
                    return new ValueTask<object>(200);
                }
                if (auth2.Token.StartsWith("TestClient"))
                {
                    AllClient.Add(auth2.Token, this);
                    return new ValueTask<object>(200);
                }
                else
                {
                    return new ValueTask<object>(404);
                }
            case string str:
                return new ValueTask<object>(str);
            default:
                break;
        }
        return NullResult;
    }

    protected override void ProcessBody(in ReadOnlySequence<byte> bodyBytes, int RpcID, short CMD, int MessageID, object options)
    {
        if (LogRecvBytes)
        {
            var len = bodyBytes.Length;
            log.Dispatcher.Invoke(() =>
            {
                log.Content += $"\n 收到bodyBytes len:{len} rpcID：{RpcID} CMD:{CMD}  MessageID:{MessageID}";
            });
        }

        base.ProcessBody(bodyBytes, RpcID, CMD, MessageID, options);
    }

    protected override void OnDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
    {
        disCount++;
        log.Dispatcher.Invoke(() =>
        {
            log.Content += $"\n 网络已断开。调用次数{disCount} \n {error} -- {activeOrPassive}";
        });
    }

    public bool IsSocketSending { get; }

    public void StartSocketSend()
    {
        throw new NotImplementedException();
    }

    public void StopSocketSend()
    {
        throw new NotImplementedException();
    }
}