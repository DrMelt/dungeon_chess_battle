# DungeonChessBattle.Protocol

网络契约与网络约定常量的共享库，所属分组 Shared。服务端与客户端两侧共用，消除端口、协议帧与字段长度魔法值。职责边界见 `functional_boundary/05`。

大厅网络契约（Hub 方法名与大厅 DTO）已拆分为独立库 `DungeonChessBattle.Lobby.Protocol`，见 `functional_boundary/19`。

## 契约机制

- **房间端口帧布局**：`[0xDC 包头][0x10 消息类型][消息体]`。`ReliableMessageFrame` 统一写头/读体，两端不再手写帧偏移。0xDC 是 LES 二进制协议包头，0x10 是服务器可靠消息类型。
- **网络默认值**：`NetworkDefaults` 定义大厅端口 `LobbyPort=10170`、默认连接密钥 `ConnectionKey=DungeonChessBattle`（配置服务器密码时以密码替换）。
- **字段长度约束**：`EntityConstants` 定义玩家名（16）、单位配置键（32）上限，服务端校验与客户端 UI 限制共享同一常量；默认副本键归 GameConfig。
- **进程间契约**：`ServerProcessEnv` 定义跨进程环境变量名（密码/父 PID），客户端 `ServerProcessHost` 写入、服务器入口与父进程看护读取。

## 最小连接契约

- `IClientConnection`：`IsConnected` / `Disconnect` / `Update` 三个成员，供门面 `GameClientService` 统一驱动大厅与房间两端；连接建立与完全断开事件由两端各自实现。

