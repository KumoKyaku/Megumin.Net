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



