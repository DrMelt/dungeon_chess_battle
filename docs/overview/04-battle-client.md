# DungeonChessBattle.Battle.Client

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。职责边界见 `functional_boundary/04`。

## 网络栈与实体管理

- 连接建立时创建 `ClientEntityManager`（包头 0xDC），用 `CountingNetPeer` 装饰 LES peer 采集出站流量；订阅 `BattleRoomEntity`、`UnitPawn`、`UnitController` 三类实体的构造事件（`callOnExisting: true` 补拉已同步实体）。
- 收到控制器实体即视为本地控制器（OnlyForOwner 分发，不依赖 `IsLocalControlled` 构造时序），用于输入提交与施法请求。

## 帧处理

`UpdateAfterPollEvents` 每帧依次：

1. 副本键同步后就绪时 `EnsureBattleScene` 构建在线 `BattleScene`（含本地 `PhysicsMovementScene`）；未同步时随下一帧重试。
2. `EntityManager.Update()` 驱动 LES 实体同步，其间 `ClientBattleLoop.VisualUpdate`（LocalSingleton）在渲染帧把 SyncVar 读数回填领域单位；其 `Update`/`LateUpdate` 为空实现。
3. 轮询 `UnitPawn` 聚焦 SyncVar，更新本地聚焦映射。
4. 每秒流量统计结算。
5. 轮询 `BattleRoomEntity` 阶段变化（`RoomState.Phase`）：触发 `BattlePhaseChanged`（LES 无公开 Changed 事件，无需镜像）。

## 同步结构

单一真相源为 `BattleScene`（Battle.Logic）→ `BattleUnit` 领域实体，服务端与在线/回放共用，不依赖网络载体。

- **下行回填**：在线端不跑本地结算。`ClientBattleLoop.VisualUpdate` 每渲染帧 `BattleSceneMirror.Pull` 一次，把 `UnitPawn` 的 `Value` 覆写进领域 `BattleUnit` 作展示源；`Update`/`LateUpdate` 空实现，`Flush` 无调用点。移动、读条、Buff、伤害与敌方 AI 只在服务端结算。
- **展示契约**：`IUnitUiView` / `IBuffUiView`（Battle.Shared.Combat）是 UI 唯一取数口径，在线与回放都以 `BattleUnit` 作为其实现。
- **契约分层**：`IWorldPoseView`（逻辑位置）→ `ISkillCasterView`（施法判定子集）；`IUnitUiView`（展示位置）与 `ISkillCasterView` 共享公共面（身份/数值/技能源），位置语义一致（在线为下行回填值，回放为本地结算值）。客户端施法预判与 UI 取同源位置，不再分离插值/权威。

在线链：`ClientBattleLoop.VisualUpdate`（`Mirror.Pull` 回填 SyncVar 读数）→ `RoomBattleClient.Units`（本地 `BattleScene.BattleUnits`）→ UI。

回放链：`ReplayEngine` 构建 `BattleScene`（不投影）每帧确定性重跑，移动在 `BattleScene.Tick` 内结算，直接读 `BattleUnit`（实现 `IUnitUiView`）供同一套展示契约消费。

两链都收敛到 `IUnitUiView`：回放"领域→`BattleUnit`→UI"每帧本地结算；在线"服务端结算→`UnitPawn`→`Mirror.Pull` 回填→UI"，显示读数即下行读数。

## 收包分流

- 先经 `ReliableMessageFrame` 识别可靠消息帧（0xDC + 0x10 类型头）：解码 `ReliableBattleEventLog` → `BattleEventCoder` 逐条解码为领域事件 → 存入 `BattleEventLogStore`（含接收时刻）并触发 `BattleEventsReceived`。连接内可靠有序，断线期间事件不补发。
- 其余 0xDC 帧交 LES 反序列化。

## 事件日志仓库

- `BattleEventLogStore` 保存当前房间会话全部事件，`GetEventLog()` 只读暴露、`GetEventLogVersion()` 版本号在会话重置（断线/重连/离开）时自增，UI 据此做增量消费与历史回填。

