# DungeonChessBattle.Protocol

网络契约唯一权威来源，所属分组 Shared。服务端与客户端两侧共用，消除协议字符串、端口与字段长度魔法值。职责边界见 `functional_boundary/05`。

## 契约机制

- **Hub 方法名常量**：客户端 `InvokeAsync` 与服务端 `[HubMethodName(HubMethods.Xxx)]` 绑定同一常量，方法名编译期对齐。
- **房间端口帧布局**：`[0xDC 包头][0x10 消息类型][消息体]`。`ReliableMessageFrame` 统一写头/读体，两端不再手写帧偏移。0xDC 是 LES 二进制协议包头，0x10 是服务器可靠消息类型。
- **网络默认值**：大厅端口 `LobbyPort=10170`、房间端口池起点 `10171`、默认连接密钥 `DungeonChessBattle`（配置服务器密码时以密码替换）。
- **字段长度约束**：`EntityConstants` 定义玩家名（16）、单位配置键（32）上限，服务端校验与客户端 UI 限制共享同一常量；默认副本键归 GameConfig。
- **进程间契约**：`ServerProcessEnv` 定义跨进程环境变量名（密码/父 PID），客户端 `ServerProcessHost` 写入、服务器入口与父进程看护读取。

## DTO 分层

- 请求与结果：`CreateRoomRequest`/`JoinRoomRequest`/`ReconnectRoomRequest` 等请求 DTO + `LobbyResult`/`LoginResult` 等结果 DTO。房间 ID 与玩家名一律服务端权威，客户端不提交或反查。
- 房间快照 `RoomSnapshot`：服务端组装单发的房间权威视图（配置 + 准备状态 + 单位），客户端以它为准。
- 回放 DTO：摘要列表 `ReplayListResult` 与下载凭证 `ReplayDownloadResult`。
- 回放数据契约归 Replay.Shared，见 `functional_boundary/18`。

## 最小连接契约

- `IClientConnection`：`IsConnected` / `Disconnect` / `Update` 三个成员，供门面 `GameClientService` 统一驱动大厅与房间两端；连接建立与完全断开事件由两端各自实现。

