//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Text;
//using System.Threading.Tasks;

//namespace Megumin.Remote.Simple
//{
//    public class LogExampleRemote : TcpRemote
//    {
//        public override ValueTask<RpcResult> SendSafeAwait<RpcResult>(object message, object options = null, Action<Exception> onException = null)
//        {
//            if (onException == null)
//            {
//                //保存原始调用处堆栈。
//                string stack = new StackTrace().ToString();
//                onException += (ex) =>
//                {
//                    string err = ex + stack;
//                    Debug.Write(err);
//                };
//            }
//            return base.SendSafeAwait<RpcResult>(message, options, onException);
//        }
//    }
//}


