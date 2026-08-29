# DungeonChessBattle.Battle.Shared

契约与数据结构层，所属分组 Shared。定义战斗、Buff、仇恨、移动、阵营、事件、敌人决策所需的数据类型与端口契约，零项目引用，无网络与 Godot 依赖。职责边界见 `functional_boundary/06`。运行时规则在 Battle.Logic。

## 数据结构

- 战斗单位 `BattleUnit`（含 `UnitCombatState`：读条目标、Buff 权威列表、冷却、仇恨表）为领域数据实体；外部载体 `UnitPawn` SyncVar 由服务端 `BattleStateSynchronizer` 单向投影，状态本身不参与网络。
- 只读消费入口 `IBattleUnitView`：AI、施法/目标校验与仇恨规则只读消费。结算输入为只读快照 `UnitSnapshot`。

## 端口契约

- `IBuffEffect`/`ISkillEffect`：效果策略端口，由内容层 GameConfig 实现，定义经 `Effect` 引用注入。
- `IUnitIntelligence`：敌人决策契约，默认实现 `EnemyIntelligence` 位于 Battle.Logic。
- `IHateRule`：以自身为中心的仇恨求值契约。
- `IMovementScene`：移动空间载体契约，`PhysicsMovementScene` 实现。

## 纯数据固有计算

- `RangeShape` 几何判定、`VectorMath`、`CampRelationResolver` 阵营映射。

## 规则归属

- 数值公式（`DamageProcessor`/`HealProcessor`）、施法静态判定（`SkillCastValidator`/`SkillTargetValidator`）、Buff 叠加与节拍（`BuffService`/`BuffTickProcessor`）、敌人决策默认实现（`EnemyIntelligence`）、战斗编排与推进（`BattleScene`）均位于 Battle.Logic。


