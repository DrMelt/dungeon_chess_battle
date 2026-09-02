# 客户端连接域内部机制

覆盖 `DungeonChessBattle.Client` 门面、`Lobby.Client`、`Battle.Client` 与 `Client.Shared`。连接状态机的跨域时序见 `flow/connection-reconnect`，下行同步链见 `flow/battle-state-sync`；模块边界见 `functional_boundary/02`、`03`、`04`、`20`。

## 门面

- 持有 `LobbyClient`（SignalR）与 `RoomBattleClient`（LiteNetLib + LES）两个持久实例，经 `IClientConnectionFactory` 创建——该接口是传输类型的唯一实例化点。实例只存在于门面内部，对外一律给抽象：房间链路给 `IClientBattleSession` 契约与 `RoomNetworkStatus` 快照，大厅链路给 `Request*` 方法。门面公开签名不含传输类型，连接发起权力由门面独占。
- 主线程驱动：LiteNetLib `NetManager` 非线程安全，SignalR 后台线程回调一律入队 `_mainThreadActions`，由 Godot 主线程每帧 `Update` 消费后再驱动两端网络轮询。所有对房间客户端的操作收敛主线程。
- 状态机、超时兜底、三条进房路径、断线重连与 `ResetToNonRoomState` 收敛见 `flow/connection-reconnect`。
- 不含回放获取：`Replay.Client` 直连服务端 HTTP 端点，门面不转发回放请求，只经 `SessionToken` 透传 `LobbyClient` 持有的会话凭证。
- 另存 `LobbyPort`：`Port` 会随进入房间重定向变成房间端口，而与大厅同宿主同端口的 HTTP 端点（回放）地址不随之变。

## 大厅客户端

- 构建 `HubConnection` 连 `http://{host}:{port}/lobby`，注册服务端广播回调（房间快照、准备→战斗重定向）。请求模式统一：`RunHubCall` 检查连接状态后 fire-and-forget `InvokeAsync`，成功/失败结果经事件回调返回。回调全部发生在 SignalR 后台线程，消费方负责转主线程。
- 连接代际 `_connectionVersion`：每次 `Connect` 递增，`StartAsync` 异步完成后检查代际是否过期，隔离旧连接的迟到回调干扰新连接。
- 缓存每个房间最近一次完整快照（`ConcurrentDictionary`），进房初始化经 `TryGetRoomSnapshot` 读取；断开/重连时清空。
- 会话凭证与登录会话同生命周期：连接断开即撤销，重连后必须重新登录才换发新凭证，同一连接再次登录撤销旧凭证、不留双活身份。它让身份走出 SignalR，当前唯一消费方是回放，大厅与房间业务一概不看它被谁消费（签发侧机制见 `overview/lobby`）。

## 房间客户端

- 连接建立时创建 `ClientEntityManager`（包头 `0xDC`），用 `CountingNetPeer` 装饰 LES peer 采集出站流量；订阅 `BattleRoomEntity`、`UnitPawn`、`UnitController` 三类实体的构造事件（`callOnExisting: true` 补拉已同步实体）。收到控制器实体即视为本地控制器（`OnlyForOwner` 分发，不依赖 `IsLocalControlled` 的构造时序），用于输入提交与施法请求。
- 每帧 `UpdateAfterPollEvents` 依次：副本键同步后就绪时 `EnsureBattleScene` 构建在线 `BattleScene`（含本地 `PhysicsMovementScene`），未同步随下一帧重试；`EntityManager.Update()` 驱动 LES 同步，其间 `ClientBattleLoop.VisualUpdate` 每渲染帧把 SyncVar 读数回填领域单位（含聚焦目标），其 `Update`/`LateUpdate` 为空实现；结算每秒流量统计；轮询 `BattleRoomEntity` 的 `RoomState.Phase` 变化触发 `BattlePhaseChanged`（LES 无公开 Changed 事件，无需镜像）。
- 收包分流：`OnNetworkReceiveInternal` 先识别可靠消息帧（`0xDC` + `0x10` 类型头），解码 `ReliableBattleEventLog` → `BattleEventCoder` 逐条解码为领域事件 → 存入 `BattleEventLogStore` 并触发 `BattleEventsReceived`；其余 `0xDC` 帧交 LES 反序列化。
- 在线端不跑移动以外的任何结算：移动、读条、冷却与预输入排队、Buff、伤害与敌方 AI 只在服务端结算，下行读数即展示源（通道方向见 `flow/battle-state-sync`）。
- `BattleEventLogStore` 保存当前房间会话全部事件，`GetEventLog()` 只读暴露，`GetEventLogVersion()` 版本号在会话重置（断线/重连/离开）时自增，UI 据此做增量消费与历史回填。连接内可靠有序，断线期间事件不补发。
- 对外唯一可见面 `IClientBattleSession : IClientBattleService, IBattleViewSource`：在两个既有契约之上补本地玩家语义（`LocalUnit` 与读领域回填态 `FocusTarget` 的 `LocalFocus`）与房间权威元信息（`DungeonKey`/`BattleStartUnixTime`）。本地玩家成员不进 `IBattleViewSource`——回放无本地控制器；连接生命周期成员一律不入该契约。
- 断线/重连时 `ClearRoomSessionState` 清空单位索引、阶段与本地网络 ID，随连接状态重建为干净会话；服务端载体不销毁，解绑先于移除的约束见 `overview/battle`。
