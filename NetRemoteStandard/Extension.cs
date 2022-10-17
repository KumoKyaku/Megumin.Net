using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Net.Remote;

/// <summary>
/// 接口默认实现，需要接口显示调用。不如扩展函数实现方便。
/// 为了方便导入，不使用命名空间，只要不撞类名就没问题。
/// </summary>
public static class NetRemoteExtension_1BF96CF42E7249EE9EBE611C57770D7C
{
    ///<inheritdoc cref="ISendAsyncable.SendAsync{T, Result}(T, object)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<(Result result, Exception exception)> SendAsync<Result>(this ISendAsyncable send,
                                                                                    object message,
                                                                                    object options = null)
    {
        return send.SendAsync<object, Result>(message, options);
    }

    ///<inheritdoc cref="ISendAsyncable.SendAsyncSafeAwait{T, Result}(T, object, Action{Exception})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result> SendAsyncSafeAwait<Result>(this ISendAsyncable send,
                                                               object message,
                                                               object options = null,
                                                               Action<Exception> onException = null)
    {
        return send.SendAsyncSafeAwait<object, Result>(message, options, onException);
    }
}




