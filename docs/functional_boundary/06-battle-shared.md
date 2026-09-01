# DungeonChessBattle.Battle.Shared

契约与数据结构层，所属分组 Shared。定义战斗、Buff、仇恨、移动、阵营、事件、敌人决策所需的数据类型与端口契约，零项目引用，无网络与 Godot 依赖。

## 职责

- 契约与数据结构：`SkillDefinition`/`BuffDefinition`/`RangeShape`/`UnitSnapshot`/`BattleUnit`/`PlayerCommand`/领域事件族与端口接口。
- 端口契约：`IBuffEffect`/`ISkillEffect`、`IBattleUnitView`、`IBattleSceneView`、`IUnitIntelligence`、`IHateRule`、`IMovementScene` 等。
- 纯数据固有计算保留：`RangeShape` 几何判定、`VectorMath`、`CampRelationResolver` 阵营映射。
- 回放结算逻辑修订号 `BattleLogicRevision`：Battle.Logic 结算时序与事件顺序的版本指纹，供录制端写入归档、重放端与客户端门控比对。它只是一个常量，规则本身与递增义务在 Battle.Logic，见 `functional_boundary/07`。
- 写权限边界：internal 成员经 `InternalsVisibleTo` 只授 Battle.Logic，构成「战斗世界可写、其余程序集不可写」的输入写面。

## 边界外

- 不含任何运行时规则：数值公式、施法校验、Buff 叠加/节拍、AI 决策、战斗编排均在 Battle.Logic。
- 不依赖网络与 Godot，不做序列化与网络载体。
- 不猜未知关系：阵营组合未覆盖时返回未知。

## 依赖

- 无：纯 .NET 类库，零项目引用。

