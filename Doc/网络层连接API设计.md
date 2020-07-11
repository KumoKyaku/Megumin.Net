当前网络连接API设计
```cs
/// <summary>
/// <para>异常在底层Task过程中捕获，返回值null表示成功，调用者不必写try catch</para>
/// </summary>
/// <param name="endPoint"></param>
/// <param name="retryCount">重试次数，失败会返回最后一次的异常</param>
/// <returns></returns>
Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0);
```

真实使用代码示例

```cs
static async Task<Server> Connect(JsonDocument info)
{
    Server server = new Server();
    IPEndPoint endPoint = new IPEndPoint(addr, port);
    var res = await server.ConnectAsync(endPoint, 1);
    if (res != null)
    {
        Console.WriteLine(res + "连接异常");
        return null;
    }
    Console.WriteLine($"连接服务器成功 <====");
    return server;
}

```


这样设计究竟有没有意义？
还是单纯设计为
```cs
Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0);
```

伪代码
```cs
static async Task<Server> Connect(JsonDocument info)
{
    Server server = new Server();
    IPEndPoint endPoint = new IPEndPoint(addr, port);
    try
    {
        await server.ConnectAsync(endPoint, 1);
    }
    catch (Exception e)
    {
        Console.WriteLine(e + "连接异常");
        return null;
    }

    Console.WriteLine($"连接服务器成功 <====");
    return server;
}
```

两种设计那种更优？


多方思考后使用抛异常设计


