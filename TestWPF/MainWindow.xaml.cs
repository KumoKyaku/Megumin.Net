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
        private EchoTcp server;

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
            this.监听状态.Content = $"开始监听";
        }

        private async void Listen(TcpRemoteListener remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(Create);
            Listen(remote);
            this.监听状态.Content = $"收到连接";
            server = re;
        }

        public static EchoTcp Create()
        {
            return new EchoTcp();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            client = new TestRemote();
            client.UI = this;
            client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 54321));
            result1.Content = $"连接成功";
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            server.Send(new TestPacket1());
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var resp = await client.SendSafeAwait<TestPacket2>(new TestPacket2() { Value = 100 });
            if (resp.Value == 100)
            {
                result1.Content = $"Rpc测试成功";
            }
            else
            {
                result1.Content = $"Rpc测试失败";
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
    }
}

public class TestRemote : TcpRemote
{
    public MainWindow UI { get; internal set; }
    protected async override ValueTask<object> OnReceive(short cmd, int messageID, object message)
    {
        UI.Dispatcher.Invoke(() =>
        {
            switch (message)
            {
                case TestPacket1 packet1:
                    UI.result1.Content = $"收到{nameof(TestPacket1)}";
                    break;
                case TestPacket2 packet2:
                    UI.result1.Content = $"收到{nameof(TestPacket2)}";
                    break;
                default:
                    break;
            }
        });

        switch (message)
        {
            case TestPacket1 packet1:
                return new TestPacket2 { Value = packet1.Value };
            case TestPacket2 packet2:
                return null;
            default:
                break;
        }
        return null;
    }

    protected override void PostDisconnect(SocketError error = SocketError.SocketError, ActiveOrPassive activeOrPassive = ActiveOrPassive.Passive)
    {
        UI.result1.Content = $"网络已断开 \n {error} -- {activeOrPassive}";
    }
}