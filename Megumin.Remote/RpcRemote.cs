using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace Megumin.Remote
{
    /// <summary>
    /// 支持Rpc功能的
    /// <para></para>
    /// 没有设计成扩展函数或者静态函数是方便子类重写。
    /// </summary>
    /// <remarks>一些与RPC支持相关的函数写在这里。</remarks>
    public abstract class RpcRemote : RemoteBase, IObjectMessageReceiver
    {
        public RpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);

        /// <summary>
        /// 分流普通消息和RPC回复消息
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual ValueTask<object> DiversionProcess(int rpcID, short cmd, int messageID, object message)
        {
            if (rpcID < 0)
            {
                //这个消息是rpc返回（回复的RpcID为负数）
                RpcCallbackPool?.TrySetResult(rpcID, message);
                return NullResult;
            }
            else
            {
                //这个消息非Rpc返回
                //普通响应onRely
                return OnReceive(cmd, messageID, message);
            }
        }

        protected override async void DeserializeSuccess(int rpcID, short cmd, int messageID, object message)
        {
            //消息处理程序的返回对象
            object reply = null;

            var trans = UseThreadSchedule(rpcID, cmd, messageID, message);
            if (trans)
            {
                reply = await Push2MessageThreadTransducer(rpcID, cmd, messageID, message);
            }
            else
            {
                reply = await DiversionProcess(rpcID, cmd, messageID, message);

                if (reply is Task<object> task)
                {
                    reply = await task;
                }

                if (reply is ValueTask<object> vtask)
                {
                    reply = await vtask;
                }
            }

            if (reply != null)
            {
                Reply(rpcID * -1, reply);
            }
        }

        /// <summary>
        /// 推到线程转化器中
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="cmd"></param>
        /// <param name="messageID"></param>
        /// <param name="message"></param>
        /// <remarks>独立一个函数，不然<see cref="MessageThreadTransducer.Push(int, short, int, object, IObjectMessageReceiver)"/>继承者无法调用</remarks>
        protected IMiniAwaitable<object> Push2MessageThreadTransducer(int rpcID, short cmd, int messageID, object message)
        {
            return MessageThreadTransducer.Push(rpcID, cmd, messageID, message, this);
        }

        ValueTask<object> IObjectMessageReceiver.Deal(int rpcID, short cmd, int messageID, object message)
        {
            return DiversionProcess(rpcID, cmd, messageID, message);
        }

        /// <summary>
        /// 将一个Rpc应答回复给远端
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="replyMessage"></param>
        protected abstract void Reply(int rpcID, object replyMessage);
    }
}
