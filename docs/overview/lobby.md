# 大厅域内部机制

覆盖 `Lobby.Server`、`Lobby.Client`、`Lobby.Protocol` 与 `Lobby.Shared`。连接与重连的跨域时序见 `flow/connection-reconnect`；模块边界见 `functional_boundary/12`、`03`、`19`、`17`。门面的主线程模型与连接状态机不在本域，见 `overview/client`；房间状态、会话凭证与回放归档的存储机制不在本域，见 `overview/datastore`。

## 调用链与身份

- `LobbyHub`（`[HubMethodName(HubMethods.Xxx)]` 绑定 `Lobby.Protocol` 常量，与客户端 `InvokeAsync` 同一常量，方法名编译期对齐）→ `ILobbyApplication`（`GameServer` 协调门面）→ `GameLobby` 大厅业务 / `IBattleRoomManager` 战斗编排；广播经 `ILobbyBroadcaster` 端口注入，实现 `SignalRBroadcaster` 映射到 SignalR Group（进房/退房/发房）。
- 身份只从登录会话反查：连接建立后客户端必须先 `Login` 登记 connectionId → 登录名，房间创建/加入/准备/重连一律据此取权威玩家名，不信任客户端自报。DTO 层同调——房间 ID 与玩家名一律服务端权威，客户端不提交也不反查。
- 连接断开 `ConnectionLostAsync`：先清登录会话，再按连接归属清理房间成员并广播最新快照。

## 快照与准备阶段

- 任何准备阶段状态变更 → `BroadcastRoomSnapshotAsync` 从 Store 组装完整 `RoomSnapshot`（配置 + 玩家准备状态 + 单位）→ `ILobbyBroadcaster` → SignalR Group 单发。客户端以该快照为唯一权威视图，不做本地增量。
- 开始战斗三重校验：发起者是房主、除房主外全员就绪、全员已选单位 → `StartRoomBattle`（等待房间线程首帧初始化完成才返回端口）→ 广播 `OnPrepareBattleRedirect` 给全房间（含端口）。
- 重连登记：登录会话反查身份 → 校验房间密码 → `TryGetRoomPort` → `RegisterPlayer` → 返回端口。`RegisterPlayer` 仅当房间已有同名会话才允许。
- `RoomStatus`（Waiting / InProgress / Finished）在存储模型与协议 DTO 间共用同一类型，避免跨层枚举映射；结束状态由战斗阶段推导。

## 会话凭证

- 登录成功后 `IssueSessionToken(connectionId)` 换发一个随机串，随 `LoginResult.SessionToken` 回客户端。换发即撤销、撤销随登录会话作废的口径在 `overview/datastore` 的身份与会话凭证一节，对客户端只剩一个后果：重连必须重新登录才换得到新凭证。
- 它让身份走出 SignalR：服务端 HTTP 端点（当前只有回放）凭请求头自证，经 `IPlayerIdentityResolver` 换成玩家记录主键，不必再借大厅连接。大厅不解释谁在消费它——`GameServer` 构造依赖里没有回放项，`LobbyHub` 上也没有回放方法。
- 加固有边界：凭证可换发可撤销，比客户端自报玩家名强一层，但 Hub 上的业务身份仍是自报的，这层加固不覆盖那里。

## 大厅客户端

- 构建 `HubConnection` 连 `http://{host}:{port}{HubPaths.Lobby}`，注册服务端广播回调（房间快照、准备→战斗重定向）。请求模式统一：`RunHubCall` 检查连接状态后 fire-and-forget `InvokeAsync`，成功/失败结果经事件回调返回。回调全部发生在 SignalR 后台线程，转主线程由门面负责。
- 连接代际 `_connectionVersion`：每次 `Connect` 递增，`StartAsync` 异步完成后检查代际是否过期，隔离旧连接的迟到回调干扰新连接。重连先清快照缓存再重建。
- 缓存每个房间最近一次完整快照（`ConcurrentDictionary`），进房初始化经 `TryGetRoomSnapshot` 读取；断开与重连时清空。服务端签发的会话凭证也留存在本层，经 `SessionToken` 透传给上层。
