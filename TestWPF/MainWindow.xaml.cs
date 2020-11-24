using Megumin.Remote;
using Megumin.Remote.Simple;
using Megumin.Remote.Test;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TestWPF;

namespace TestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TestRemote client;
        private TestRemote server;

        public MainWindow()
        {
            InitializeComponent();
            MessageLUT.Regist(new TestPacket1());
            MessageLUT.Regist(new TestPacket2());
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TcpRemoteListener listener = new TcpRemoteListener(54321);
            Listen(listener);
            this.Serverlog.Content += $"\n 开始监听";
        }

        private async void Listen(TcpRemoteListener remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var accept = await remote.ListenAsync(Create);
            Listen(remote);
            this.Serverlog.Content += $"\n 收到连接";
            server = accept;
        }

        public TestRemote Create()
        {
            return new TestRemote() { log = this.Serverlog };
        }

        private void CreateClient(object sender, RoutedEventArgs e)
        {
            client = new TestRemote();
            client.log = this.ClientLog;
            client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 54321));
            ClientLog.Content += $"\n 连接成功";
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
    }
}

public class TestRemote : TcpRemote
{
    public Label log { get; set; }
    protected async override ValueTask<object> OnReceive(short cmd, int messageID, object message)
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
                default:
                    break;
            }
        });

        switch (message)
        {
            case TestPacket2 packet2:
                return message;
        }
        return null;
    }

    protected override void PostDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
    {
        log.Dispatcher.Invoke(() =>
        {
            log.Content += $"\n 网络已断开 \n {error} -- {activeOrPassive}";
        });
    }
}