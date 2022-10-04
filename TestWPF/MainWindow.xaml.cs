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

namespace TestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TestRemote client;
        private TestRemote server;
        private TcpRemoteListener listener;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int port = 54321;
            int.TryParse(ListenPort.Text, out port);
            listener?.Stop();
            listener = new TcpRemoteListener(port);
            Listen(listener);
            this.Serverlog.Content += $"\n 开始监听";
        }

        private async void Listen(TcpRemoteListener remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var accept = await remote.ListenAsync(Create);
            Listen(remote);
            if (accept != null)
            {
                this.Serverlog.Content += $"\n 收到连接 {accept.Client.RemoteEndPoint} ";
                server = accept;
            }
        }

        private void StopListen_Click(object sender, RoutedEventArgs e)
        {
            listener?.Stop();
        }

        public TestRemote Create()
        {
            var r = new TestRemote() { log = this.Serverlog };
            r.LogRecvBytes = this.LogRecvBytes.IsChecked ?? false;
            return r;
        }

        private void CreateClient(object sender, RoutedEventArgs e)
        {
            int port = 54321;
            int.TryParse(ConnectPort.Text, out port);
            IPAddress targetIP = IPAddress.Loopback;
            IPAddress.TryParse(TargetIP.Text, out targetIP);

            client = new TestRemote();
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
            server.LogRecvBytes = true;
        }

        private void LogRecvBytes_Unchecked(object sender, RoutedEventArgs e)
        {
            server.LogRecvBytes = false;
        }

        private void StartSocketSend_Click(object sender, RoutedEventArgs e)
        {
            client.StartSocketSend();   
        }

        private void StopSocketSend_Click(object sender, RoutedEventArgs e)
        {
            client.StopSocketSend();
        }
    }
}

public class TestRemote : TcpRemote
{
    static Dictionary<string, TestRemote> AllClient = new Dictionary<string, TestRemote>();
    public Label log { get; set; }
    public bool LogRecvBytes { get; internal set; }

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
                    log.Content += $"\n 收到{message.GetType().Name} value:{message}";
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

    protected override void ProcessBody(in ReadOnlySequence<byte> bodyBytes, object options, int RpcID, short CMD, int MessageID)
    {
        if (LogRecvBytes)
        {
            var len = bodyBytes.Length;
            log.Dispatcher.Invoke(() =>
            {
                log.Content += $"\n 收到bodyBytes len:{len} rpcID：{RpcID} CMD:{CMD}  MessageID:{MessageID}";
            });
        }

        base.ProcessBody(bodyBytes, options, RpcID, CMD, MessageID);
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