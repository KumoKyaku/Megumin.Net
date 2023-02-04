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
            //client.Connect(new IPEndPoint(IPAddress.Loopback, 54321));
            client.Connect(new IPEndPoint(IPAddress.Parse("192.168.1.150"), 54321));
            client.Blocking = true;
            client.SendBufferSize = 1024;
            LogAppend("连接成功");
        }

        public void LogAppend(string message)
        {
            Log.Dispatcher.Invoke(() =>
            {
                Log.Text = message + "\n" + Log.Text;
            });
        }

        const int buffercount = 1024 * 1024 * 100;
        byte[] buffer = new byte[buffercount];

        /// <summary>
        /// TODO, 本机测试可能有问题。使用2个机器测试。
        /// 
        /// Q：为什么明明接收端没有Recv，这里也不会阻塞？
        /// https://stackoverflow.com/questions/59018603/where-the-data-stores-before-we-invoke-socket-readbuffer-offset-count/59024582#59024582
        /// A：足够大的情况，还是会阻塞的。这个大小时如何设置的？
        /// ~~并不是ReceiveBufferSize。ReceiveBufferSize实际上是滑动窗口大小。~~不确定，完全混乱搞不清楚。
        /// 
        /// 接收端大小设置4096，如果这个值是内核缓冲区，那么发送将会阻塞。
        /// 实际结果并不会阻塞，而是会发送成功。
        /// 应该是内核缓冲区大小。接收端收到数据，程序不从IO中取出，当IO满后，将不在能接收。
        /// 
        /// 
        /// 一些推测，发送内核缓冲区 是自适应的，只要未满，第一个发送总是不会阻塞。
        /// 但是内核缓冲区不是无限增大的。具体多少大小没有找到资料。也没有找到设置API。
        /// 
        /// 对于 接收内核缓冲区，应该也是自适应的。但不是无限增大的。接收内核缓冲区满了，发送端就会阻塞。
        /// 合理的写法是，总是尽可能的将 接收内核缓冲区 的数据读取出来 到 用户侧。
        /// 
        /// Q：对于流控功能，即使接收端不Recv，内核缓冲区仍然能接收 大约 2MB左右的数据？
        /// A：这样看流控基本没用。
        /// 
        /// 内核缓冲区大小通常应为与带宽一致。
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

        /// <summary>
        /// 测试何时才能阻塞 
        /// 2,200kb左右时阻塞。每次都不一样，但相差不多。 
        /// 但是结果和上面单次发送不同，上面单次发送10MB，也能发送成功。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void WhileSend_Click(object sender, RoutedEventArgs e)
        {
            LogAppend($"WhileSend_Click 开始发送 {logcount}");
            byte[] buffer = new byte[1024 * 10]; 
            int totalSend = 0;
            while (true)
            {
                var sendBytes = client.Send(buffer);
                totalSend += sendBytes;
                LogAppend($"WhileSend_Click 发送总字节{totalSend}");
                await Task.Delay(10);
            }
        }
    }
}
