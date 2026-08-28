# DungeonChessBattle.Lobby.Server

大厅服务器，所属分组 Server。承载大厅 SignalR 传输端点、业务实现与协调门面，经 `IBattleRoomManager` 契约编排战斗房间生命周期。职责边界见 `functional_boundary/12`。

## 调用链

`LobbyHub`（网络端点，`[HubMethodName]` 绑定协议常量）→ `ILobbyApplication`（`GameServer` 协调门面）→ `GameLobby`（大厅业务）/ `IBattleRoomManager`（战斗编排）。广播经 `ILobbyBroadcaster` 端口注入实现。

## 身份反查机制

- 连接建立后客户端必须先 `Login` 登记登录会话（connectionId → 登录名）。房间创建/加入/准备/重连一律从登录会话反查权威玩家名，不信任客户端自报。
- 连接断开 `ConnectionLostAsync`：先清登录会话，再按连接归属清理房间成员并广播最新快照。

## 快照广播流

- 任何准备阶段状态变更 → `GameLobby.BroadcastRoomSnapshotAsync` 从 Store 组装完整 `RoomSnapshot`（配置 + 玩家准备状态 + 单位）→ `ILobbyBroadcaster` → SignalR Group 单发。客户端以该快照为唯一权威视图。

## 战斗编排

- 开始战斗：`HandleStartBattleAsync` 校验发起者是房主、除房主外全员就绪、全员已选单位 → `IBattleRoomManager.StartRoomBattle`（等待房间线程首帧初始化）→ 广播 `OnPrepareBattleRedirect` 给全房间（含端口）。
- 断线重连：`HandleReconnectRoomAsync` 登录会话反查身份 → 校验房间密码 → `TryGetRoomPort` → `RegisterPlayer`（仅房间既有同名会话才允许，杜绝冒用他人 playerId）→ 返回端口，客户端凭 playerId 直连房间端口。
- 重连的身份依据是登录会话，只恢复房间既有同名会话。

## 会话凭证签发

- 登录成功后 `_stateStore.IssueSessionToken(connectionId)` 换发一个随机串，随 `LoginResult.SessionToken` 回给客户端；同一连接再次登录会撤销旧凭证，不留双活身份。
- 凭证让身份能走出 SignalR：服务端 HTTP 端点（当前只有回放）凭请求头自证，经 `IPlayerIdentityResolver` 换成玩家记录主键。大厅不知道也不关心谁在消费它。
- 撤销随登录会话：`ConnectionLostAsync` 清登录会话时一并作废，连接断开即失效。
- 大厅不持有任何回放规则：`GameServer` 的构造依赖里没有回放项，`LobbyHub` 上也没有回放方法。

## 传输实现

- `SignalRBroadcaster` 把 `ILobbyBroadcaster` 映射到 SignalR Group（AddToRoom/RemoveFromRoom/SendToRoom）。
- `LobbyServerConfig` 注入服务器密码切片。

