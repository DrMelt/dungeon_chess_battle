# 客户端回放设计

回放的本质是：一份自包含字节流 + 一段可复现的确定性重算，在无网络、无实时输入、无权威服务器参与下，原样复现一场已结束战斗的完整表现。观看者不提供输入。

本文跨 `Replay.Shared`、`Replay`、`Battle.Server`、`Game` 多模块，描述回放的确定性与两链收敛约定。模块职责见 `functional_boundary/16`、`18`，在线并行的同步链路见 `battle-state-sync.md`。

## 单一真相源复用

- 权威状态由 `BattleScene`（`Battle.Logic`）持有的领域实体 `BattleUnit` 决定，服务端与回放共用，不依赖网络载体。
- 回放 `ReplayEngine` 构建 `BattleScene` 时**不注入投影器与移动桥**，移动由引擎本地按 `BattleMovementResolver` + `PhysicsMovementScene` 结算（等价服务端 `UnitPawn.Update`）。
- 领域层与展示契约一致：在线经"领域→`SyncVarProjector`→LES→载体→领域回填→`BattleUnit`→UI"，回放"领域→`BattleUnit`→UI"，都收敛到 `IUnitUiView`/`IBuffUiView`，UI 不感知来源。仅在线多一层投影/回填。

## 确定性契约

输入重放的成立依赖确定性与数据一致，二者都必须是契约而非假设：

- 逻辑确定性：AI/伤害/移动均为纯函数无随机（`Battle.Logic/Shared/Server` 无 `Random`），固定逻辑步长，可以重算断言。
- 内容一致性：`ReplayRecordHeader.DataVersion` 为录制端 `GameConfigDB.DataRevision`；客户端构建 `ReplayEngine` 时校验，不匹配拒绝重放。任何影响战斗结果的配置变化都必须递增 `DataRevision`，把数据演化导致的旧回放静默漂移变成声响失败。
- 格式版本：`ReplayFormatVersion.Current` 门控记录模型，模型或编码变化时递增，`ReplayRecordCoder` 解码校验。

播放控制：`ReplayCoordinator` 以固定步长累积器驱动引擎，`SeekTo` 重置到首帧快进。

## 预留改进

- 事件反馈消费：`ReplayEngine.Step()` 返回的 `IBattleEvent` 流已由 `Game` 侧复用在线 `UnitStateChangeInfo` 消费，弹出与在线共用的受击/治疗/Buff 浮字；倍速下逐帧消费的观感与在线插值平滑度仍待补。
- 关键帧快照：`SeekTo` 反向跳现为 O(n) 从首帧快进；后续以周期 keyframe 快照就近重建，跳转与确定性校验都可从任意点起步。
- 展示插值：回放逐 tick 直读权威位，低 tick 观感跳格；如需与在线渲染同平滑度，补渲染层插值。
- 输入管线集中化：录制分散在 `BattleRoomServer.TryRecord*` 旁路，后续收敛为统一输入管线，录制作为透明观察者。
- 记录体积与截断：`BattleReplayRecorder` 条目上限 `Complete=false` 静默截断，需明确截断语义并压缩体积。
