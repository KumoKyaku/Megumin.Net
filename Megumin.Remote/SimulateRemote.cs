using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Megumin.Remote
{
    /// <summary>
    /// 模拟一个远端
    /// </summary>
    public class SimulateRemote : RpcRemote
    {
        public SimulateRemote Target { get; protected set; }
        /// <summary>
        /// 模拟延迟
        /// </summary>
        public int Delay { get; set; }

        public void SimulateConnect(SimulateRemote remote)
        {
            remote.Target = this;
            Target = remote;
        }

        public override async void Send<T>(T message, int rpcID, object options = null)
        {
            await Task.Delay(Delay).ConfigureAwait(false);
            Target.DeserializeSuccess(rpcID, GetCmd(options), -1, message, options);
        }

        public override void SetTransport<T>(T transport)
        {
            throw new NotSupportedException();
        }
    }
}




