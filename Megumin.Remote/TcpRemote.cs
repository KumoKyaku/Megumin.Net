﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Net.Remote;
using Megumin.Message;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ByteMessageList = Megumin.Remote.ListPool<System.Buffers.IMemoryOwner<byte>>;
using System.IO.Pipelines;
using System.Threading;

namespace Megumin.Remote
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal static class ListPool<T>
    {
        static ConcurrentQueue<List<T>> pool = new ConcurrentQueue<List<T>>();

        /// <summary>
        /// 默认容量10
        /// </summary>
        public static int MaxSize { get; set; } = 10;

        public static List<T> Rent()
        {
            if (pool.TryDequeue(out var list))
            {
                if (list == null || list.Count != 0)
                {
                    return new List<T>();
                }
                return list;
            }
            else
            {
                return new List<T>();
            }
        }

        /// <summary>
        /// 调用者保证归还后不在使用当前list
        /// </summary>
        /// <param name="list"></param>
        public static void Return(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            if (pool.Count < MaxSize)
            {
                list.Clear();
                pool.Enqueue(list);
            }
        }

        public static void Clear()
        {
            while (pool.Count > 0)
            {
                pool.TryDequeue(out var list);
            }
        }

    }

    
    public partial class TcpRemote : RemoteBase,  IRemote
    {
        public Socket Client { get; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;

        /// <summary>
        /// Mono/IL2CPP 请使用中使用<see cref="TcpRemote.TcpRemote(AddressFamily)"/>
        /// </summary>
        public TcpRemote() : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {

        }

        /// <remarks>
        /// <para>SocketException: Protocol option not supported</para>
        /// http://www.schrankmonster.de/2006/04/26/system-net-sockets-socketexception-protocol-not-supported/
        /// </remarks>
        public TcpRemote(AddressFamily addressFamily) 
            : this(new Socket(addressFamily,SocketType.Stream, ProtocolType.Tcp))
        {

        }

        public TcpRemote(IMessagePipeline messagePipeline) : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
            MessagePipeline = messagePipeline;
        }

        public TcpRemote(IMessagePipeline messagePipeline, AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
            MessagePipeline = messagePipeline;
        }

        /// <summary>
        /// 使用一个已连接的Socket创建远端
        /// </summary>
        /// <param name="client"></param>
        internal TcpRemote(Socket client)
        {
            this.Client = client;
            IsVaild = true;
        }

        void OnSocketException(SocketError error)
        {
            TryDisConnectSocket();
            OnDisConnect?.Invoke(error);
        }

        void TryDisConnectSocket()
        {
            try
            {
                if (Client.Connected)
                {
                    Client.Shutdown(SocketShutdown.Both);
                    Client.Disconnect(false);
                    Client.Close();
                }
            }
            catch (Exception)
            {
                //todo
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    try
                    {
                        if (Client.Connected)
                        {
                            Disconnect();
                        }
                    }
                    catch (Exception)
                    {

                    }
                    finally
                    {
                        Client?.Dispose();
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。
                IsVaild = false;
                lock (sendlock)
                {
                    while (sendWaitList.TryDequeue(out var owner))
                    {
                        owner?.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~TcpRemote()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    ///连接 断开连接
    partial class TcpRemote:IConnectable
    {
        public event Action<SocketError> OnDisConnect;

        bool isConnecting = false;
        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            if (isConnecting)
            {
                return new Exception("Connection in progress/连接正在进行中");
            }
            isConnecting = true;
            this.ConnectIPEndPoint = endPoint;
            while (retryCount >= 0)
            {
                try
                {
                    await Client.ConnectAsync(ConnectIPEndPoint);
                    isConnecting = false;
                    ReceiveStart();
                    return null;
                }
                catch (Exception e)
                {
                    if (retryCount <= 0)
                    {
                        isConnecting = false;
                        return e;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }

            isConnecting = false;
            return new NullReferenceException();
        }

        public void Disconnect()
        {
            IsVaild = false;
            manualDisconnecting = true;
            TryDisConnectSocket();
        }
    }

    /// 发送实例消息
    partial class TcpRemote
    {
        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer) => Client.SendAsync(msgBuffer, SocketFlags.None);
    }

    /// 发送字节消息
    partial class TcpRemote
    {
        ConcurrentQueue<IMemoryOwner<byte>> sendWaitList = new ConcurrentQueue<IMemoryOwner<byte>>();
        bool isSending;
        private MemoryArgs sendArgs;
        protected readonly object sendlock = new object();

        SendStates State = SendStates.Idel;

        enum SendStates
        {
            Idel,
            MoveWaiting2Sending,
            Sending,
            Release,
        }


        /// <summary>
        /// 注意，发送完成时内部回收了buffer。
        /// ((框架约定1)发送字节数组发送完成后由发送逻辑回收)
        /// </summary>
        /// <param name="bufferMsg"></param>
        public override void SendAsync(IMemoryOwner<byte> bufferMsg)
        {
            lock (sendlock)
            {
                sendWaitList.Enqueue(bufferMsg);
            }
            SendStart();
        }

        /// <summary>
        /// 检测是否应该发送
        /// </summary>
        /// <returns></returns>
        bool CheckCanSend()
        {
            if (!Client.Connected)
            {
                return false;
            }

            ///如果待发送队列有消息，交换列表 ，继续发送
            lock (sendlock)
            {
                if (!sendWaitList.IsEmpty && !manualDisconnecting && isSending == false)
                {
                    isSending = true;
                    return true;
                }
            }

            return false;
        }

        void SendStart()
        {
            if (!CheckCanSend())
            {
                return;
            }

            if (sendArgs == null)
            {
                sendArgs = new MemoryArgs();
            }


            if (sendWaitList.TryDequeue(out var owner))
            {
                if (owner != null)
                {
                    sendArgs.SetMemoryOwner(owner);

                    sendArgs.Completed += SendComplete;
                    if (!Client.SendAsync(sendArgs))
                    {
                        SendComplete(this, sendArgs);
                    }
                }
            }
        }

        void SendComplete(object sender, SocketAsyncEventArgs args)
        {
            //UnityEngine.Debug.Log("SendComplete");
            ///这个方法由IOCP线程调用。需要尽快结束。
            args.Completed -= SendComplete;
            isSending = false;

            ///无论成功失败，都要清理发送缓冲
            sendArgs.owner.Dispose();

            if (args.SocketError == SocketError.Success)
            {
                ///冗余调用，可以省去
                //args.BufferList = null;

                SendStart();
            }
            else
            {
                SocketError socketError = args.SocketError;
                args = null;
                if (!manualDisconnecting)
                {
                    ///遇到错误
                    OnSocketException(socketError);
                }
            }
        }
    }

    

    internal class MemoryArgs : SocketAsyncEventArgs
    {
        public void SetMemoryOwner((IMemoryOwner<byte> memoryOwner,int count) sendbuffer)
        {
            if (sendbuffer.memoryOwner == null)
            {
                return;
            }

            bool TryAdd((IMemoryOwner<byte> memoryOwner,int count) buffer)
            {
                if (MemoryMarshal.TryGetArray<byte>(sendbuffer.memoryOwner.Memory, out var sbuffer))
                {
                    sendList.Add(new ArraySegment<byte>(sbuffer.Array, sbuffer.Offset, sendbuffer.count));
                    releaseList.Add(sendbuffer.memoryOwner);
                    return true;
                }

                return false;
            }


            if (!TryAdd(sendbuffer))
            {
                var temp = BufferPool.Rent(sendbuffer.count);
                sendbuffer.memoryOwner.Memory.Span.Slice(0, sendbuffer.count).CopyTo(temp.Memory.Span);
                if (!TryAdd((temp,sendbuffer.count)))
                {
                    throw new Exception($"内存池损坏");
                }
            }

        }

        public void Flush()
        {
            BufferList = sendList;
        }

        public void Release()
        {
            sendList.Clear();
            foreach (var item in releaseList)
            {
                try
                {
                    item.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            releaseList.Clear();
        }

        readonly List<IMemoryOwner<byte>> releaseList
            = new List<IMemoryOwner<byte>>();

        List<ArraySegment<byte>> sendList = new List<ArraySegment<byte>>();
    }

    /// <summary>
    /// 全新版本
    /// </summary>
    public class TcpRemote2
    {
        Socket Client { get; }
        RpcCallbackPool RpcCallbackPool { get; }
        public async void ReceiveStart()
        {
            var pipe = new Pipe();
            FillPipe(pipe.Writer);
            ReadPipe(pipe.Reader);
        }

        private async void FillPipe(PipeWriter pipeWriter)
        {
            var buffer = pipeWriter.GetMemory(512);
            var count = await Client.ReceiveAsync(buffer, SocketFlags.None);
            pipeWriter.Advance(count);
            _ = pipeWriter.FlushAsync();
            FillPipe(pipeWriter);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pipeReader"></param>
        private async void ReadPipe(PipeReader pipeReader)
        {
            var result = await pipeReader.ReadAsync();
            var buffer = result.Buffer;

            while (buffer.Length > 4)
            {
                var length = buffer.ReadInt();//包体长度
                if (buffer.Length >= length + 4)
                {
                    ///取得消息体
                    var body = buffer.Slice(4, length);

                    ProcessBody(body, null);
                    //标记已使用数据
                    var pos = result.Buffer.GetPosition(length + 4);
                    pipeReader.AdvanceTo(pos);

                    buffer = buffer.Slice(length + 4);//切除已使用部分
                }
                else
                {
                    break;
                }
            }

            //继续处理
            ReadPipe(pipeReader);
        }

        /// <summary>
        /// 处理一个完整的消息包
        /// </summary>
        void ProcessBody(in ReadOnlySequence<byte> byteSequence, object options = null)
        {
            //读取RpcID 和 消息ID
            var (RpcID, MessageID) = Read(byteSequence);
            if (TryDeserialize(MessageID, byteSequence.Slice(8), out var message, options))
            {
                Deal2(RpcID, MessageID, message);
            }
            else
            {

            }
        }

        public async void Deal2(int rpcID, int messageID, object message)
        {
            var post = true;//转换线程

            //消息处理程序的返回对象
            object reply = null;

            if (post)
            {
                reply = await MessageThreadTransducer.Push(rpcID, message, this);
            }
            else
            {
                reply = await Deal(rpcID, messageID, message);

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
                Reply(rpcID, reply);
            }
        }

        /// <summary>
        /// 回复给远端
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="replyMessage"></param>
        private void Reply(int rpcID, object replyMessage)
        {
            throw new NotImplementedException();
        }

        public ValueTask<object> Deal(int rpcID,int messageID, object message)
        {
            if (rpcID < 0)
            {
                //这个消息是rpc返回（回复的RpcID为负数）
                RpcCallbackPool?.TrySetResult(rpcID, message);
                return new ValueTask<object>(result: null);
            }
            else
            {
                ///这个消息是非Rpc应答
                ///普通响应onRely
                return DealMessage(message);
            }
        }

        /// <summary>
        /// 通常用户接收反序列化完毕的消息的函数
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual ValueTask<object> DealMessage(object message)
        {
            return new ValueTask<object>(result: null);
        }



        (int RpcID, int MessageID) Read(in ReadOnlySequence<byte> byteSequence)
        {
            unsafe
            {
                Span<byte> span = stackalloc byte[8];
                byteSequence.CopyTo(span);
                return (span.ReadInt(), span.Slice(4).ReadInt());
            }
        }

        public bool TryDeserialize(int messageID, in ReadOnlySequence<byte> byteSequence, out object message, object options = null)
        {
            try
            {
                message = MessageLUT.Deserialize(messageID, byteSequence, options);
                return true;
            }
            catch (Exception)
            {
                //log todo
                message = default;
                return false;
            }
        }

        Queue<IMemoryOwner<byte>> sendQ;

        /// <summary>
        /// 取得下一个待发送队列。
        /// </summary>
        /// <returns></returns>
        Task<IMemoryOwner<byte>> PeekNext()
        {
            throw new NotImplementedException();
        }

        public void Send()
        {

        }

        public async void SendStart()
        {
            var target = await PeekNext();
            var length = target.Memory.Length;
            var result = await Client.SendAsync(target.Memory, SocketFlags.None);
            if (result == length)
            {
                //dequeue?
                //成功？
                target.Dispose();
            }

            SendStart();
        }

        public bool TreSer(IBufferWriter<byte> writer, int rpcID, object message, object options = null)
        {
            try
            {
                var span = writer.GetSpan(8);
                writer.Advance(8);
                int messageID = MessageLUT.Serialize(writer, message, options);
                rpcID.WriteTo(span);
                messageID.WriteTo(span.Slice(4));
                return true;
            }
            catch (Exception)
            {
                //todo log;
                return false;
            }
        }

        public bool TreSer(IBufferWriter<byte> writer, int rpcID, in ReadOnlySequence<byte> sequence, object options = null)
        {
            try
            {
                var span = writer.GetSpan(4);
                writer.Advance(4);

                foreach (var item in sequence)
                {
                    writer.Write(item.Span);
                }

                rpcID.WriteTo(span);
                return true;
            }
            catch (Exception)
            {
                //todo log;
                return false;
            }
        }
    }
}
