# 大厅域内部机制

覆盖 `Lobby.Server`、`Lobby.Protocol`、`Lobby.Shared`、`Server.DataStore` 与 `Server.DataStore.Shared`。连接与重连的跨域时序见 `flow/connection-reconnect`；模块边界见 `functional_boundary/12`、`19`、`17`、`11`、`10`。

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

- 登录成功后 `IssueSessionToken(connectionId)` 换发一个随机串，随 `LoginResult.SessionToken` 回客户端；同一连接再次登录撤销旧凭证，不留双活身份；撤销随登录会话，连接断开即作废。
- 它让身份走出 SignalR：服务端 HTTP 端点（当前只有回放）凭请求头自证，经 `IPlayerIdentityResolver` 换成玩家记录主键，不必再借大厅连接。大厅不解释谁在消费它——`GameServer` 构造依赖里没有回放项，`LobbyHub` 上也没有回放方法。
- 加固有边界：凭证可换发可撤销，比客户端自报玩家名强一层，但 Hub 上的业务身份仍是自报的，这层加固不覆盖那里。

## 存储并发策略

- 房间级锁表 `GetRoomLock`：每房间一个常驻锁对象，串行化同房间读改写，保护 `List<T>` 操作与可变模型字段。锁对象不随房间删除回收——回收会让新旧锁错位竞态。
- 对外读接口返回深拷贝（`CloneRoom` / 快照），阻止调用方绕过房间锁改写 Store 内可变对象。
- 跨房间枚举（`ListActiveRooms`）不加锁，依赖 `ConcurrentDictionary` 弱一致性快照，字段可能略旧可接受。
- 门面组合 `IGameStateStore` = `IRoomStateStore` + `IPlayerStateStore`，业务层只面向门面，存储引擎在装配层替换。并发语义：任何线程都可调用，同房间读改写由实现保证原子。
- 不入本模型的东西：网络连接密钥（属网络层）、战斗房间会话（属房间私有）、战斗单位状态（由领域 `BattleUnit` 权威持有）、回放归档存储契约（在 `Server.Abstractions`，实现 `InMemoryReplayStore` 在本层）。

## 关键语义

- 成员移除 `RemovePlayerByConnection` 分两套：准备阶段（Waiting）执行人数扣减、单位清理、房主转让，最后一人退出删除房间全部状态；战斗中（InProgress）只做基础清理，生命周期归 `BattleRoomManager`。
- 准备状态与准备单位增删都校验「已准备不可改」，未选单位不可准备；房主身份不参与准备判定，房主退出即转让。
- 玩家记录注册表进程内只增不删，登录名 → 主键派生；同名玩家共享主键导致回放互见，属已知局限。
- 会话凭证、玩家记录主键与回放归档都只在进程内有效：服务端重启后同名玩家派生新主键，旧主键只残留在客户端本地副本里，不再可比。`PlayerIdentityResolver` 凭证无效或所属连接已登出时返回 null 且不登记记录，避免匿名凭证凭空建主键。
