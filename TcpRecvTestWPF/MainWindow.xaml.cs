using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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

namespace TcpRecvTestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket server;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Listen_Click(object sender, RoutedEventArgs e)
        {
            TcpListener tcpListener = new TcpListener(54321);
            tcpListener.Start();
            LogAppend("开始监听");
            server = await tcpListener.AcceptSocketAsync();
            server.ReceiveBufferSize = 4096;
            LogAppend("收到连接");
        }

        public void LogAppend(string message)
        {
            Log.Dispatcher.Invoke(() =>
            {
                Log.Text += message + "\n";
            });
        }

        const int buffercount = 1024 * 10000;
        byte[] buffer = new byte[buffercount];
        private void BeginRecv_Click(object sender, RoutedEventArgs e)
        {
            server.BeginReceive(buffer, 0, buffercount, SocketFlags.None, RecvCallback, buffer);
        }

        private void RecvCallback(IAsyncResult ar)
        {
            int recvCount = server.EndReceive(ar);
            byte[] buffer = ar.AsyncState as byte[];
            LogAppend($"接收 {buffer[0]} {ar.IsCompleted} {recvCount}");

        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            Log.Dispatcher.Invoke(() =>
            {
                Log.Text = null;
            });
        }
    }
}
