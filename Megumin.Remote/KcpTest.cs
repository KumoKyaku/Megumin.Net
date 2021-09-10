//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.Net.Sockets.Kcp;
//using System.Text;

//namespace Megumin.Remote
//{
//    public class KcpTest:RpcRemote
//    {
//        IKcpIO kcp = null;
//        IKcpUpdate kcpUpdate = null;
//        protected int KcpIOChannel { get; set; }
//        const int BufferSizer = 1024 * 4;
//        IBufferWriter<byte> BufferWriter;

//        public void InitKcp(int kcpChannel)
//        {
//            if (kcp == null)
//            {
//                KcpIOChannel = kcpChannel;
//                KcpIO kcpIO = new KcpIO((uint)KcpIOChannel);
//                kcp = kcpIO;
//                kcpUpdate = kcpIO;
//                KcpOutput();
//                KCPRecv();
//            }
//        }

//        async void KcpOutput()
//        {
//            while (true)
//            {
//                await kcp.Output(BufferWriter).ConfigureAwait(false);
//                var (buffer, lenght) = BufferWriter.Pop();
//                SocketSend(buffer, lenght);
//            }
//        }

//        private void SocketSend(object buffer, object lenght)
//        {
//            throw new NotImplementedException();
//        }

//        protected override void Send(int rpcID, object message, object options = null)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}






