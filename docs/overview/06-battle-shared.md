# DungeonChessBattle.Battle.Shared

契约与数据结构层，所属分组 Shared。定义战斗、Buff、仇恨、移动、阵营、事件、敌人决策所需的数据类型与端口契约，零项目引用，无网络与 Godot 依赖。职责边界见 `functional_boundary/06`。运行时规则在 Battle.Logic。

## 数据结构

- 战斗单位 `BattleUnit`（含 `UnitCombatState`：读条目标、Buff 权威列表、冷却、仇恨表）为领域数据实体，`FocusTarget` 是其中的持续展示态；与外部载体 `UnitPawn` 的字段搬运在 `Battle.Entities` 的同步通道内完成，本层不依赖网络与框架类型。
- 网络回填占位 `NetworkBuffDefinition`/`NoOpBuffEffect`：在线端把下行 Buff 还原为 `ActiveBuff` 展示壳所用的占位定义，效果永不触发，客户端不推进 Buff。
- 只读消费入口 `IBattleUnitView`：AI、施法/目标校验与仇恨规则只读消费。结算输入为只读快照 `UnitSnapshot`。
- 写权限约定：本层 `internal` 成员即「Battle.Logic 可写、其余程序集不可写」的面（`InternalsVisibleTo`），当前是 `BattleUnit.MoveInput` 与 `BattleUnit.CastInput` 两个 setter——写者全在 Battle.Logic：宿主一律经输入门面 `BattleIntentHub`，AI 决策直写单位字段；两类意图在 `Tick` 末统一作废。`BattleUnit.FocusTarget` 不在该面内：它与生命值同规格，服务端由门面写、在线端由同步通道回填。
- 玩家输入形状 `PlayerCommand`：移动、施法、聚焦三类输入的同一扁平形态，键为 `UnitId`，字段与线上请求载荷和回放记录条目同构。`CastTargetPos` 承载「单位目标丢弃位置锚点」这条唯一口径；本类型只声明形状，合法性判定在门面。
- ID 口径与命名：单位 ID 在领域、命令与仇恨账本内一律 `UnitId`（成员名 `SourceUnitId`/`TargetUnitId`/`UnitId`），0 恒非法即 `UnitId.None`（LES 同步实体 ID 从 1 起分配）。SyncVar 与 MessagePack 不认包装类型，线协议、同步实体与回放条目恒为原生 `ushort`，这类字段一律用 `…NetId` 命名——后缀即"此处是原生承载，未进强类型"。领域内部不再有裸 ID 中转，收放只发生在命令构造点（服务端请求转发、`ReplayCommands`）与同步编解码点（`BattleEventCoder`、`UnitPawn.StateSync`）。
- 按帧意图载荷 `CastIntent`：只带技能键、已解析的目标引用与位置锚点，不带裁定结论，射程与阵营一律在消费点按当时位置判定。
- 结算逻辑修订号 `BattleLogicRevision.Value`：Battle.Logic 结算时序与事件顺序的指纹，与内容侧 `GameConfigDB.DataRevision` 并列构成回放的双重门控。落本层只因它要同时被录制端、重放端与客户端门控读到，而三者的公共下游只有这里；本层不含规则本身，递增义务见 `overview/07`。

## 端口契约

- `IBuffEffect`/`ISkillEffect`：效果策略端口，由内容层 GameConfig 实现，定义经 `Effect` 引用注入。
- `IUnitIntelligence`：敌人决策契约，默认实现 `EnemyIntelligence` 位于 Battle.Logic。
- `IHateRule`：以自身为中心的仇恨求值契约。
- `IMovementScene`：移动空间载体契约，`PhysicsMovementScene` 实现。

## 纯数据固有计算

- `RangeShape` 几何判定、`VectorMath`、`CampRelationResolver` 阵营映射。

## 规则归属

- 数值公式（`DamageProcessor`/`HealProcessor`）、施法静态判定（`SkillCastValidator`/`SkillTargetValidator`）、Buff 叠加与节拍（`BuffService`/`BuffTickProcessor`）、敌人决策默认实现（`EnemyIntelligence`）、战斗编排与推进（`BattleScene`）均位于 Battle.Logic。


