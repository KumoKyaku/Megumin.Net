using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
        EndPoint RemoteEndPoint { get; }
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
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <remarks>没有timeout参数，可以调用<see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/></remarks>
        Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default);
        //超时API设计
        //Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, int timeoutMillonseconds = 30000);
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
    /// Tcp 由数据网络切换到WiFi网络时，会触发一个ConnectReset。
    /// </summary>
    public interface IDisconnectHandler
    {
        /// <summary>
        /// 当网络连接已经断开, 发送和接受可能有一个没有完全停止。
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// <param name="error"></param>
        /// <param name="options"></param>
        /// <remarks>主要用于通知外部停止继续发送</remarks>
        void PreDisconnect(SocketError error, object options = null);

        /// <summary>
        /// 断开连接之后
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="options"></param>
        /// <remarks>可以用于触发重连，并将现有发送缓冲区转移到心得连接中</remarks>
        void OnDisconnect(SocketError error, object options = null);

        /// <summary>
        /// 断开连接之后
        /// <para>todo 这个函数没有处理线程转换</para>
        /// </summary>
        /// /// <param name="error"></param>
        /// <param name="options"></param>
        void PostDisconnect(SocketError error, object options = null);
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
        void Send<T>(T message, object options = null);
        ///// <summary>
        ///// 发送消息，无阻塞立刻返回
        ///// </summary>
        ///// <param name="byteMessage"></param>
        //void SendAsync(IMemoryOwner<byte> byteMessage);
    }

    /// <summary>
    /// 可以发送一个消息并期待一个指定类型的返回值
    /// </summary>
    /// <remarks>
    /// 为了通用性和框架兼容性，object message导致值类型装箱是可以妥协的。
    /// Result已经是泛型了，如果message也使用泛型，则需要使用2个泛型，调用出没办法自动类型推导，需要明确指定，严重影响易用性。
    /// <para/> --------
    /// <para/> 在这里吐槽几句，可能有人觉得这两个API比较像Go。https://www.zhihu.com/question/451484968
    /// <para/> 其实本质含义不一样,设计初衷是禁止异常抛出到Send处。
    ///         基于异步的业务逻辑应该是顺畅的，不应该写try-catch。总不能每个Send位置都去catch SocketException。
    /// <para/> 但是在网络模块底层拦截所有异常也是不行的，某些业务逻辑有可能需要明确知道异常是什么。
    ///         所以折衷将异常以返回值的形式传递回调用者处。
    /// <para/> 这里的精髓是，当后续代码读取结果时，结果一定符合预期，如果不符合预期，后续代码则不会被执行。
    ///         当出现异常时，允许不触发异步延续，后续代码执行全被吃掉，这是Go所不具备的。
    /// <para/> <see cref="SendAsyncSafeAwait{T, Result}(T, object, Action{object, Exception})"/> 才是设计的最终目的。
    ///         而<see cref="SendAsync{T, Result}(T, object)"/>只是对特殊需求的补丁API。
    /// </remarks>
    public interface ISendAsyncable
    {
        /// <summary>
        /// 异步发送消息，封装Rpc过程。
        /// </summary>
        /// <typeparam name="T">发送消息类型</typeparam>
        /// <typeparam name="Result">期待的Rpc结果类型，如果收到返回类型，但是类型不匹配，返回null</typeparam>
        /// <param name="message">发送消息的类型需要序列化 具体实现使用查找表 MessageLUT 中指定ID和序列化函数</param>
        /// <param name="options">参数项，在整个发送管线中传递</param>
        /// <returns>需要检测空值</returns>
        /// <remarks>
        /// <para/> 当message为引用类型时，使用扩展方法，不用指定泛型 <typeparamref name="T"/>。
        /// <para/> 当message为值类型时，显示指定 <typeparamref name="T"/>，大多数情况可以避免装箱。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask<(Result result, Exception exception)> SendAsync<T, Result>(T message, object options = null);

        /// <summary>
        /// 异步发送消息，封装Rpc过程
        /// 结果值是保证有值的，如果结果值为空或其他异常,触发异常回调函数，不会抛出异常，所以不用try catch。
        /// 异步方法的后续部分不会触发，所以后续部分可以省去空检查。
        /// <para>****千万注意，只有在Result有返回值的情况下，后续异步方法才会执行。
        /// 这不是语言特性，也不是语法特性。这由具体实现的类库保证。*****</para>
        /// </summary>
        /// <typeparam name="T">发送消息类型</typeparam>
        /// <typeparam name="Result"></typeparam>
        /// <param name="message"></param>
        /// <param name="options">参数项，在整个发送管线中传递</param>
        /// <param name="onException">发生异常时的回调函数。当Result转型失败时，object为真实返回值，其他情况object为null</param>
        /// <returns></returns>
        /// <remarks>
        /// <para/> 当message为引用类型时，使用扩展方法，不用指定泛型 <typeparamref name="T"/>。
        /// <para/> 当message为值类型时，显示指定 <typeparamref name="T"/>，大多数情况可以避免装箱。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask<Result> SendAsyncSafeAwait<T, Result>(T message, object options = null, Action<object, Exception> onException = null);
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
    ///// 可以断线重连的,业务层不关心连接
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
        /// <summary>
        /// 最后一次收到消息的时间戳,因为Unity中时间戳是float
        /// </summary>
        DateTimeOffset LastReceiveTime { get; }
        /// <summary>
        /// 设置接受回调是个失败的设计，实际使用中无论如何都要从要给实现中继承，重写部分函数。
        /// </summary>
        //event ReceiveCallback OnReceiveCallback;
    }

    /// <summary>
    /// 应用网络层API封装
    /// <para>不需要实现<see cref="IDisposable"/>,实现起来繁琐，工程中没有太太价值。</para>
    /// <para>不应该继承IConnectable，不关心连接过程， 在语义上是Session级别的接口。
    /// 对于接收端，收到时就是已连接的，所以IConnectable是次要的。</para>
    /// </summary>
    /// <inheritdoc/>
    public interface IRemote : ISendable, IRemoteID, ISendAsyncable
    {
        ITransportable Transport { get; }
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
        int MillisecondsTimeout { get; }
    }

    public interface ICmdOption
    {
        /// <summary>
        /// <![CDATA[
        /// 1 << 0 :Echo,       
        /// 1 << 15 :Test
        /// ]]>
        /// </summary>
        /// <remarks>
        /// 没有固定标准，具体参考PreReceive函数中实现。
        /// </remarks>
        short Cmd { get; }
    }

    /// <summary>
    /// todo 只有主动链接测 不停的发送心跳,自动断线重连,回调用于UI
    /// </summary>
    interface IReconnetCallback
    {
        /// <summary>
        /// 开始断线重连
        /// </summary>
        void OnBenginReconnect();
        /// <summary>
        /// 结束断线重连
        /// </summary>
        /// <param name="result"></param>
        /// <param name="exception"></param>
        void OnEndReconnect(int result, SocketException exception);
        /// <summary>
        /// 重试次数
        /// </summary>
        int ReTryTimes { get; }
    }

    /// <summary>
    /// 对Socket进行控制。
    /// 不一定所有remote都支持。大多时候用于调试
    /// </summary>
    public interface ISocketSendable
    {
        bool IsSocketSending { get; }
        void StartSocketSend();
        void StopSocketSend();
    }

    /// <summary>
    /// 对Socket进行控制。
    /// 不一定所有remote都支持。大多时候用于调试
    /// </summary>
    public interface ISocketReceiveable
    {
        bool IsSocketReceiving { get; }
        void StartSocketReceive();
        void StopSocketReceive();
    }
}




