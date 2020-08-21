# 第二版

原则： 协议无关的定义在基类。

目前将TcpRemote 拆为3层， RemoteBase定了必要消息处理流程的函数和抽象函数。是原MessagePipeline的体现。
RpcRemote 是 实现Rpc功能的必要实现，主要处理收到消息Rpc分流问题。

前两层不涉及到数据保存。不涉及数据清理逻辑，基本与协议无关。

TcpRemote处理Socket数据相关，为最后一层。
Udp 后面只需要继承和重写就可以了。

