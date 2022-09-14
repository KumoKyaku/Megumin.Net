using Megumin.Remote;
using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RpcSource = System.Threading.Tasks.IMiniAwaitable<(object result, System.Exception exception)>;

namespace Megumin.Remote
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public class ObjectRpcCallbackPool : IntKeyObjectRpcCallbackPool
    {
    }

    /// <summary>
    /// rpc超时异常
    /// </summary>
    public class RcpTimeoutException : TimeoutException
    {
        public override string Message => MyMessage + base.Message;

        public Type RequstType { get; internal set; }
        public Type ResponseType { get; internal set; }

        string MyMessage
        {
            get
            {
                return $"Rpc请求[{RequstType?.Name}]超时，请求结果类型[{ResponseType?.Name}]";
            }
        }
    }
}
