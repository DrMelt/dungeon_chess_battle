# DungeonChessBattle.Battle.Shared

契约与数据结构层，所属分组 Shared。定义战斗、Buff、仇恨、移动、阵营、事件、敌人决策所需的数据类型与端口契约，零项目引用，无网络与 Godot 依赖。

## 职责

- 契约与数据结构：`SkillDefinition`/`BuffDefinition`/`RangeShape`/`UnitSnapshot`/`BattleUnit`/领域事件族与端口接口。
- 端口契约：`IBuffEffect`/`ISkillEffect`、`IBattleUnitView`、`IBattleSceneView`、`IUnitIntelligence`、`IHateRule`、`IMovementScene` 等。
- 纯数据固有计算保留：`RangeShape` 几何判定、`VectorMath`、`CampRelationResolver` 阵营映射。

## 边界外

- 不含任何运行时规则：数值公式、施法校验、Buff 叠加/节拍、AI 决策、战斗编排均在 Battle.Logic。
- 不依赖网络与 Godot，不做序列化与网络载体。
- 不猜未知关系：阵营组合未覆盖时返回未知。

## 依赖

- 无：纯 .NET 类库，零项目引用。

