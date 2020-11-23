using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Net.Remote
{
    /// <summary>
    /// 实例ID
    /// </summary>
    public interface IRemoteID
    {
        /// <summary>
        /// Remote唯一。
        /// </summary>
        int ID { get; }
    }

    /// <summary>
    /// 末端地址
    /// </summary>
    public interface IRemoteEndPoint
    {
        /// <summary>
        /// 连接的目标地址
        /// </summary>
        IPEndPoint ConnectIPEndPoint { get; set; }
        /// <summary>
        /// 连接后重映射的地址,Udp,Kcp可能会使用这个设计。
        /// <para>如果没有重映射, 返回<see cref="Socket.RemoteEndPoint"/> </para>
        /// </summary>
        EndPoint RemappedEndPoint { get; }
    }

    /// <summary>
    /// 可连接的
    /// </summary>
    public interface IConnectable : IRemoteEndPoint, IDisconnectable
    {
        /// <summary>
        /// 尝试连接。可以重写具体实现并将Auth过程合并在一起。
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="retryCount">重试次数，失败会返回最后一次的异常</param>
        /// <returns></returns>
        Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0);
        //todo 超时API设计
        //Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, int timeoutMillonseconds = 30000);
        //Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken token = default);
    }

    public interface IDisconnectable
    {
        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <param name="triggerOnDisConnect">是否触发OnDisConnect</param>
        /// <param name="waitSendQueue">是否等待发送队列发送完成，如果等待会，具体实现应该触发异步，不应该阻塞</param>
        void Disconnect(bool triggerOnDisConnect = false, bool waitSendQueue = false);
    }

    /// <summary>
    /// 发送任意对象，只要它能被MessageLUT解析。
    /// </summary>
    public interface ISendable
    {
        /// <summary>
        /// 发送消息，无阻塞立刻返回
        /// <para>调用方 无法了解发送情况</para>
        /// 序列化过程同步执行，方法返回表示序列化已结束，修改message内容不影响发送数据。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="options">参数项，在整个发送管线中传递</param>
        /// <remarks>序列化开销不大，放在调用线程执行比使用单独的序列化线程更好</remarks>
        void Send(object message, object options = null);
        ///// <summary>
        ///// 发送消息，无阻塞立刻返回
        ///// </summary>
        ///// <param name="byteMessage"></param>
        //void SendAsync(IMemoryOwner<byte> byteMessage);
    }



    /// <summary>
    /// 可以发送一个消息并期待一个指定类型的返回值
    /// </summary>
    /// <remarks>为了通用性和框架兼容性，object导致值类型装箱是可以妥协的。</remarks>
    public interface ISendCanAwaitable
    {
        /// <summary>
        /// 异步发送消息，封装Rpc过程。
        /// </summary>
        /// <typeparam name="RpcResult">期待的Rpc结果类型，如果收到返回类型，但是类型不匹配，返回null</typeparam>
        /// <param name="message">发送消息的类型需要序列化 具体实现使用查找表 MessageLUT 中指定ID和序列化函数</param>
        /// <param name="options">参数项，在整个发送管线中传递</param>
        /// <returns>需要检测空值</returns>
        ValueTask<(RpcResult result, Exception exception)> Send<RpcResult>(object message, object options = null);

        /// <summary>
        /// 异步发送消息，封装Rpc过程
        /// 结果值是保证有值的，如果结果值为空或其他异常,触发异常回调函数，不会抛出异常，所以不用try catch。
        /// 异步方法的后续部分不会触发，所以后续部分可以省去空检查。
        /// <para>****千万注意，只有在RpcResult有返回值的情况下，后续异步方法才会执行。
        /// 这不是语言特性，也不是语法特性。这由具体实现的类库保证。*****</para>
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="message"></param>
        /// <param name="onException">发生异常时的回调函数</param>
        /// <param name="options">参数项，在整个发送管线中传递</param>
        /// <returns></returns>
        /// <remarks></remarks>
        ValueTask<RpcResult> SendSafeAwait<RpcResult>(object message, Action<Exception> onException = null, object options = null);
    }

    //广播一定是个静态方法，没法通过接口调用
    ///// <summary>
    ///// 可以广播发送
    ///// </summary>
    //public interface IBroadCastSend
    //{
    //    /// <summary>
    //    /// 用于广播方式的发送,用于对多个远端发送相同信息。
    //    /// <para>msgBuffer 必须符合<see cref="IMessagePipeline"/>中对应的消息格式，否则接收端无法解析。</para>
    //    /// </summary>
    //    /// <param name="msgBuffer"></param>
    //    /// <returns></returns>
    //    Task BroadCastSendAsync(ArraySegment<byte> msgBuffer);
    //}

    ///// <summary>
    ///// 可以断线重连的
    ///// </summary>
    //public interface IReConnectable
    //{
    //    /// <summary>
    //    /// 打开关闭断线重连
    //    /// </summary>
    //    bool IsReConnect { get; set; }

    //    /// <summary>
    //    /// 尝试重连的最大时间，超过时间触发断开连接(毫秒)
    //    /// </summary>
    //    int ReConnectTime { get; set; }

    //    /// <summary>
    //    /// 触发断线重连
    //    /// </summary>
    //    event Action<IReConnectable> PreReConnect;
    //    /// <summary>
    //    /// 断线重连成功。重连失败触发断开连接<see cref="IConnectable.OnDisConnect"/>
    //    /// </summary>
    //    event Action<IReConnectable> ReConnectSuccess;
    //}

    /// <summary>
    ///
    /// </summary>
    /// <param name="message"></param>
    /// <param name="receiver"></param>
    /// <returns></returns>
    /// <remarks> 在接口中不要放事件</remarks>
    [Obsolete("直接从实现中继承，回调函数不在触发", true)]
    public delegate ValueTask<object> ReceiveCallback(object message, IReceiveMessage receiver);

    /// <summary>
    /// 接收消息
    /// </summary>
    /// <remarks>不要定义OnReceive函数，由于具体业务逻辑不同，这个函数的签名可能有很多中变化，不能标准化。</remarks>
    public interface IReceiveMessage
    {
        ///// <summary>
        ///// 最后一次收到消息的时间
        ///// </summary>
        //[Obsolete("DateTime 开销太大，使用时间戳代替")]
        //DateTime LastReceiveTime { get; }
        /// <summary>
        /// 最后一次收到消息的时间戳,因为Unity中时间戳是float
        /// </summary>
        float LastReceiveTimeFloat { get; }
        /// <summary>
        /// 设置接受回调是个失败的设计，实际使用中无论如何都要从要给实现中继承，重写部分函数。
        /// </summary>
        //event ReceiveCallback OnReceiveCallback;
    }

    /// <summary>
    /// 应用网络层API封装
    /// <para>不需要实现<see cref="IDisposable"/>,实现起来繁琐，工程中没有太太价值。</para>
    /// </summary>
    /// <inheritdoc/>
    public interface IRemote : IRemoteEndPoint, ISendable, IReceiveMessage,
        IConnectable, IRemoteID
        , ISendCanAwaitable
    {
        /// <summary>
        /// 实际连接的Socket
        /// </summary>
        Socket Client { get; }
        /// <summary>
        /// 当前是否正常工作
        /// </summary>
        bool IsVaild { get; }
    }

    /// <summary>
    /// 监听端多路复用，用于Udp,即一个socket 对应多个远端。
    /// </summary>
    public interface IMultiplexing
    {
        /// <summary>
        /// 负数和0 是非法值，最小值为1 。当为1时每个连接对应一个socket，等于Tcp效果。默认为1。
        /// <para>开启多路复用并不一定提高效率，要以实际测试为准</para>
        /// 设定值需要根据网络传输速度和消息处理速度决定，没有通用标准。
        /// </summary>
        int MultiplexingCount { get; set; }
    }

    /// <summary>
    /// Rpc回调池参数，具体由实现库支持
    /// </summary>
    public interface IRpcTimeoutOption
    {
        /// <summary>
        /// 指定毫秒后超时，-1表示永不超时。
        /// </summary>
        int MillisecondsDelay { get; }
    }
}
