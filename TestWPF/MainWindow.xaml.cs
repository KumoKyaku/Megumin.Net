using Megumin.Remote;
using Megumin.Remote.Echo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private static async void Listen(TcpRemoteListener remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(Create);
            Listen(remote);
        }

        public static EchoTcp Create()
        {
            return new EchoTcp() { Post2ThreadScheduler = true };
        }
    }
}
