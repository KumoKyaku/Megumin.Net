using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote.Simple
{
    /// <summary>
    /// Tcp回声远端
    /// </summary>
    public class EchoTcp:TcpRemote
    {
        protected override ValueTask<object> OnReceive(short cmd, int messageID, object message)
        {
            return new ValueTask<object>(message);
        }
    }
}
