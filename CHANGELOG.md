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

## [2.1.0] - 2022-10-04
### Added  
- 增加断线重连支持
- 增加内置认证消息
- 由发送端和消息协议控制的的应答机制
- 增加内置类型序列化
- 网卡详细信息面板
- 打包真机测试
### Changed  
- Task ConnectAsync(IPEndPoint endPoint, int retryCount = 0, CancellationToken cancellationToken = default);
### Fixed  
- 修复一个可能的接受长度错误，当接收pipe取得Memory长度等于预期值时，可能造成接受数据丢失。
- 修复TCP粘包时读取消息长度错误BUG
- 修正PDB，    `<DebugType>portable</DebugType>` 一定要是可移植的，不然unity中dll内部不能断点



