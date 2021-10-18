//using System;
//using System.Collections.Generic;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading.Tasks;
//using Megumin.Message;
//using Net.Remote;

//TODO:
//发送和接收时两条管道,和tcp,udp,kcp协议无关,将管道和协议分离,然后重连时复用管道就可以了.
//发送和接收是对两个管道的操作.
//StartWork后:
//发送消息rpc+序列化后输入发送管道,异步取出并通过Socket发送. SendI端-->SendO端
//从Socket接收数据放入接收管道,并异步取出处理消息.          RecvI端-->RecvO端
//只有手动关闭时,或者重连失败时关闭 I端,出现错误或者掉线,并不处理管道,等待重连时复用.
//逐层抽象
//连接层 rpc层 序列化层 数据管道层


//namespace Megumin.Remote
//{
//    /// <summary>
//    /// 断线重连
//    /// </summary>
//    public class ReConnector : IReConnectable
//    {
//        private bool _isReConnect;

//        public ReConnector(IRemote remote)
//        {
//            this.Remote = remote;
//        }

//        public IRemote Remote { get; }
//        public bool IsReConnect
//        {
//            get => _isReConnect;
//            set
//            {
//                if (_isReConnect != value)
//                {
//                    _isReConnect = value;

//                    SendHeartAsync();
//                }
//            }
//        }

//        readonly HeartBeatsMessage heart = new HeartBeatsMessage() { Time = DateTime.Now };
//        private async void SendHeartAsync()
//        {
//            int GetLast()
//            {
//                return (int)(DateTime.Now - Remote.LastReceiveTime).TotalMilliseconds;
//            }

//            var last = GetLast();
//            while (last < 3000)
//            {
//                await Task.Delay(Math.Max(3000 - last,10));
//                last = GetLast();
//            }

//            heart.Time = DateTime.Now;
//            var (result, complete) = await Remote.SendAsync<HeartBeatsMessage>(heart).WaitAsync(3000);

//            if (complete)
//            {
//                var (resultMessage, exception) = result;
//                switch (exception)
//                {
//                    case TimeoutException timeout:
//                    case NullReferenceException nullReference:
//                        var res = ReConnectAsync();
//                        break;
//                    default:
//                        if (IsReConnect)
//                        {
//                            SendHeartAsync();
//                        }
//                        break;
//                }
//            }
//            else
//            {
//                ///心跳丢失，开始重连
//                var res = await ReConnectAsync();
//                if (res)
//                {
//                    if (IsReConnect)
//                    {
//                        ///重连成功，继续心跳
//                        SendHeartAsync();
//                    }
//                }
//                else
//                {
//                    ///重连失败，断线
//                    Remote.Disconnect();
//                }
//            }

//        }

//        async Task<bool> ReConnectAsync()
//        {
//            PreReConnect?.Invoke(this);
//            var (result, complete) = await Remote.ConnectAsync(Remote.ConnectIPEndPoint,1).WaitAsync(ReConnectTime);
//            if (complete&& result == null)
//            {
//                ReConnectSuccess?.Invoke(this);
//                return true;
//            }
//            else
//            {
//                return false;
//            }
//        }

//        public int ReConnectTime { get; set; } = 10000;

//        public event Action<IReConnectable> PreReConnect;
//        public event Action<IReConnectable> ReConnectSuccess;



//    }
//}



