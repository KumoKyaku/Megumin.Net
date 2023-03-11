# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
## [Unreleased] - YYYY-MM-NN

### Added   
### Changed  
### Deprecated  
### Removed  
### Fixed  
### Security  
-->

---
## [Unreleased] - YYYY-MM-NN

## [3.3.0] - 2023-03-11
### Added   
- 增加Tcp发送接收测试
- 增加Tcp io测试
- 反序列化支持stream 参数类型。

### Changed  
- 删除net5.0改为net6.0
- 修复一个错误拼写 formater ->  formatter

## [3.1.0] - 2022-10-29
### Added   
- Listener没有Start时调用接收，增加一个错误日志
- 增加模拟远端，用于测试
- 增加广播方法
- 公开 TcpBufferWriter
### Changed  
- `破坏性改动`：RpcID 发送时由正数改为负数，返回时由负数改为正数。
- 0是普通消息。int.minValue是广播消息。
- 拆分 interface ISendAsyncable 改为扩展函数。
### Removed 
- 移除不必要引用
- 删除过时代码
### Fixed  
- UdpRemoteListener 延迟移除旧地址
- 修复unity监听测试
- 重构bufferwriter。增加Udp Send 序列化多线程安全。

## [3.0.0] - 2022-10-16

### Added   
- 从Remote拆分IDisconnectHandler。
- 从Remote拆分Transport。
### Changed  
- 公开大部分Remote 中间处理过程 API。
- 整理ITransportable 和 IRemote 和 IListener接口
- 重命名 interface ISendCanAwaitable -> ISendAsyncable
### Deprecated  
### Removed  
### Fixed  
- 修复Tcp半包假死BUG。
- 调整  void Send<T>(T message, int rpcID, object options = null) 参数顺序，消除Rpc   send  reply   二义性。
### Security  

## [2.1.0] - 2022-10-04
### Added  
- 增加断线重连支持
- 增加内置认证消息
- 由发送端和消息协议控制的的应答机制
- 增加内置类型序列化
- 网卡详细信息面板
- 打包真机测试
- 增加UDP Kcp支持。
### Changed  
- Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default);
### Fixed  
- 修复一个可能的接受长度错误，当接收pipe取得Memory长度等于预期值时，可能造成接受数据丢失。
- 修复TCP粘包时读取消息长度错误BUG
- 修正PDB，    `<DebugType>portable</DebugType>` 一定要是可移植的，不然unity中dll内部不能断点



