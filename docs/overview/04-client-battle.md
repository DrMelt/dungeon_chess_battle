# DungeonChessBattle.Client.Battle

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。职责边界见 `functional_boundary/04`。

## 网络栈与实体管理

- 连接建立时创建 `ClientEntityManager`（包头 0xDC），用 `CountingNetPeer` 装饰 LES peer 采集出站流量；订阅 `BattleRoomEntity`、`UnitPawn`、`UnitController` 三类实体的构造事件（`callOnExisting: true` 补拉已同步实体）。
- 收到控制器实体即视为本地控制器（OnlyForOwner 分发，不依赖 `IsLocalControlled` 构造时序），用于输入提交与施法请求。

## 帧处理

`UpdateAfterPollEvents` 每帧依次：

1. 副本键同步后就绪时 `EnsureBattleScene` 构建在线 `BattleScene`（本地 `PhysicsMovementScene` 仅结构同构，不结算移动）；未同步时随下一帧重试。
2. `EntityManager.Update()` 驱动 LES 实体同步与状态回放。
3. 逐 `UnitPawn` 调 `SyncUnit(...)` 把 SyncVar 回填领域 `BattleUnit`（位置/朝向/状态取 SyncVar `Value` 服务端权威，不做插值；Buff/冷却重建运行时壳），供 UI 统一取数。
4. 每秒流量统计结算。
5. 轮询 `BattleRoomEntity` 阶段变化：触发 `BattlePhaseChanged`（LES 无公开 Changed 事件，无需镜像）。

## 同步结构

单一真相源为 `BattleScene`（Battle.Logic）→ `BattleUnit` 领域实体，服务端与在线/回放共用，不依赖网络载体。

- **状态同步**：`IProjectableBattleState` 供状态同步器读取领域只读状态；服务端 `BattleStateSynchronizer` 写 `UnitPawn` SyncVar 与房间阶段，由 `BattleLoop.LateUpdate` 驱动；回放不投影。在线端反向把载体 SyncVar 回填领域（`RoomBattleClient`）。
- **展示契约**：`IUnitUiView` / `IBuffUiView`（Battle.Shared.Combat）是 UI 唯一取数口径，在线与回放都以 `BattleUnit` 作为其实现。
- **契约分层**：`IWorldPoseView`（权威位置）→ `ISkillCasterView`（施法判定子集）；`IUnitUiView`（展示位置）与 `ISkillCasterView` 共享公共面（身份/数值/技能源），位置语义一致（服务端权威）。客户端施法预判与 UI 取同源位置，不再分离插值/权威。

在线链：`BattleLoop.LateUpdate` 的 `Tick` → `BattleStateSynchronizer` 写 `UnitPawn` SyncVars → LES 下发 → 客户端 `SyncUnit` 回填 `BattleUnit` → `RoomBattleClient.Units` → `BattleSessionContext.Units` → UI。计数型字段直接写（LES diff），冷却/Buff/仇恨内容比对节流重建 SyncList，倒计时写 `EndServerTick`。

回放链：`ReplayEngine` 构建 `BattleScene`（不投影）每帧确定性重跑，移动在 `BattleScene.Tick` 内结算，直接读 `BattleUnit`（实现 `IUnitUiView`）供同一套展示契约消费。

两链都收敛到 `IUnitUiView`：在线经"领域→UnitPawn→网络→镜像→UI"，回放"领域→BattleUnit→UI"，领域层与展示契约一致，仅在线多一层投影/反投影。

## 收包分流

- 先经 `ReliableMessageFrame` 识别可靠消息帧（0xDC + 0x10 类型头）：解码 `ReliableBattleEventLog` → `BattleEventCoder` 逐条解码为领域事件 → 存入 `BattleEventLogStore`（含接收时刻）并触发 `BattleEventsReceived`。连接内可靠有序，断线期间事件不补发。
- 其余 0xDC 帧交 LES 反序列化。

## 事件日志仓库

- `BattleEventLogStore` 保存当前房间会话全部事件，`GetEventLog()` 只读暴露、`GetEventLogVersion()` 版本号在会话重置（断线/重连/离开）时自增，UI 据此做增量消费与历史回填。

## 确定性移动预测

- 每个 `UnitPawn` 注入 `MovementResolver.Move` + 本地物理场景，与服务端注入同一实现：客户端本地预测即时反馈消除 RTT 卡顿，服务端权威经 LES 回滚重放自动纠偏。

