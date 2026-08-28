# DungeonChessBattle.Lobby.Protocol

大厅网络契约唯一权威来源，所属分组 Shared。服务端与客户端两侧共用，消除大厅协议字符串与 DTO 魔法值。职责边界见 `functional_boundary/19`。

## 契约机制

- **Hub 方法名常量**：客户端 `InvokeAsync` 与服务端 `[HubMethodName(HubMethods.Xxx)]` 绑定同一常量，方法名编译期对齐。

## DTO 分层

- 请求与结果 DTO：`CreateRoomRequest`/`JoinRoomRequest`/`ReconnectRoomRequest` 等请求，`LobbyResult`/`LoginResult` 等结果。房间 ID 与玩家名一律服务端权威，客户端不提交或反查。
- `LoginResult.SessionToken`：登录成功时服务端签发的连接级会话凭证，随登录会话作废。它让身份能延伸到服务端 HTTP 端点，当前唯一消费方是回放，大厅不解释它被谁用。
- 房间快照 `RoomSnapshot`：服务端组装单发的房间权威视图（配置 + 准备状态 + 单位），客户端以它为准。
- 回放契约不在本层：DTO、路由与序列化约定见 Replay.Protocol。
