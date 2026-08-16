# DungeonChessBattle.Battle.Domain

纯领域模型，所属分组 Shared。战斗结算、Buff、仇恨、移动、阵营、事件与数学的领域类型唯一权威定义层，无网络与 Godot 依赖。

## 职责范围

- 领域类型：`BattlePhase`、`SkillDefinition` 族与 `SkillKeyId`、`SkillTargetValidator`、`BuffView`、`CombatTypes`、`IBattleUnit` 投影接口、`HateEffect`。
- 仇恨规则族、Buff 实例模型、战场布局与移动场景抽象、阵营关系解析、领域事件流、向量数学与范围判定形状。

## 不负责

- 不依赖网络与 Godot，不做序列化与网络载体。
- 不实现具体编排，战斗推进与 AI 由 Battle.Logic 承担。
- 不猜未知关系：阵营组合未覆盖时返回 `Unknown`。

## 依赖项

- 无：纯 .NET 类库，零项目引用。
