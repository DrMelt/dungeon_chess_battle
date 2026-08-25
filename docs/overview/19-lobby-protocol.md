# DungeonChessBattle.Lobby.Protocol

大厅网络契约唯一权威来源，所属分组 Shared。服务端与客户端两侧共用，消除大厅协议字符串与 DTO 魔法值。职责边界见 `functional_boundary/19`。

## 契约机制

- **Hub 方法名常量**：客户端 `InvokeAsync` 与服务端 `[HubMethodName(HubMethods.Xxx)]` 绑定同一常量，方法名编译期对齐。

## DTO 分层

- 请求与结果：`CreateRoomRequest`/`JoinRoomRequest`/`ReconnectRoomRequest` 等请求 DTO + `LobbyResult`/`LoginResult` 等结果 DTO。房间 ID 与玩家名一律服务端权威，客户端不提交或反查。
- 房间快照 `RoomSnapshot`：服务端组装单发的房间权威视图（配置 + 准备状态 + 单位），客户端以它为准。
- 回放 DTO：摘要列表 `ReplayListResult` 与下载凭证 `ReplayDownloadResult`。
- 回放数据契约归 Replay.Shared，见 `functional_boundary/18`。
