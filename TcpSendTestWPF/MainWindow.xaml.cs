using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

namespace TcpSendTestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket client;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 54321));
            client.Blocking = true;
            client.SendBufferSize = 1024;
            LogAppend("连接成功");
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
        /// <summary>
        /// Q：为什么明明接收端没有Recv，这里也不会阻塞？
        /// A：足够大的情况，还是会阻塞的。这个大小时如何设置的？并不是ReceiveBufferSize。
        /// 应该时IO缓冲大小。接收端收到数据，程序不从IO中取出，当IO满后，将不在能接收。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Send1000_Click(object sender, RoutedEventArgs e)
        {
            logcount++;
            buffer[0] = logcount;
            LogAppend($"Send1000_Click 开始发送 {logcount}");
            var sendBytes = client.Send(buffer);
            LogAppend($"Send1000_Click 发送字节{sendBytes}");
        }

        byte logcount = 0;
        private void LogTest_Click(object sender, RoutedEventArgs e)
        {
            logcount++;
            LogAppend($"LogTest_Click 显示Log {logcount}");
        }

        private void SendAsync_Click(object sender, RoutedEventArgs e)
        {
            logcount++;
            buffer[0] = logcount;
            SendAsync2();
            LogAppend($"SendAsync_Click 发送调用结束");
        }

        private async void SendAsync2()
        {
            LogAppend($"SendAsync_Click 开始发送 {logcount}");
            var sendBytes = await client.SendAsync(buffer);
            LogAppend($"SendAsync_Click 发送字节{sendBytes}");
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
