# DungeonChessBattle.Battle.Domain

纯领域模型，所属分组 Shared。战斗结算、Buff、仇恨、移动、阵营、事件、敌人决策与数学的领域类型唯一权威定义层，无网络与 Godot 依赖。职责边界见 `functional_boundary/06`。

## 依赖倒置通道

- 领域权威状态由战斗世界自持的 `BattleUnit`（`UnitCombatState`）承载：读条目标、Buff 权威列表、冷却权威列表、仇恨表。外部载体 `UnitPawn` SyncVar 经 `IBattleProjector` 单向投影，状态本身不参与网络。
- 只读消费入口 `IBattleUnitView`：AI、施法/目标校验与仇恨规则只读消费；`BattleUnit` 与 `UnitPawn` 各自实现，读写能力只保留在领域实体具体类。
- 结算输入为只读快照 `UnitSnapshot`，数值结算为纯函数，无副作用。

## 施法静态判定唯一来源

- `SkillCastValidator.CanCast` 聚合归属、状态（存活/非读条/冷却就绪）、目标/位置与阵营关系判定。服务端权威校验、客户端预输入拦截与 AI 决策三端共用同一实现，规则不漂移。
- 冷却剩余经 `IBattleUnitView.GetTotalCooldownRemaining`（全局冷却与个体冷却取较大者），以截止 tick 换算。

## 敌人决策闭环

- `IUnitIntelligence.Decide(自我, IBattleSceneView 只读视图, 阵营关系函数)` → 纯数据 `EnemyDecision`（Idle / MoveTo / CastSkill）。实现必须无状态，实例可多单位共享。
- `BattleScene.ApplyDecisions` 逐单位触发决策，动作经战斗世界统一调度：移动输入写 `MoveInput` 并经移动桥输出，施法请求经 `BeginCast` 发起。单位不感知场景实现。
- 默认实现 `EnemyIntelligence`：仇恨最高者优先选目标，无仇恨回退最近者；停靠距离取技能射程配置。

## 仇恨求值模型

- `IHateRule.Evaluate` 以自身为中心评估领域事件（伤害/治疗/仇恨请求），只产出本单位的 `HateEffect`。目标对象为中心，无关事件直接不产效果。
- `HateTable` 是单位权威仇恨账本：Add / Multiply / SetTop（嘲讽）/ RemoveTarget / Clear，变更经投影器内容比较节流同步。
- `HateSettings` 提供全局参数，`HateFactor` 为单位级倍率。

## 领域事件流

- `IBattleEvent` 纯数据事件族（DamageOccurred / HealOccurred / BuffApplied / CastStarted / CastCanceled / CastCompleted / UnitDied 等），是网络广播与仇恨推衍的单一真相源。

## 阵营与移动

- 阵营为多值字符串列表（`CampConstants`），敌我判定经 `CampRelationResolver` 委托函数由副本配置定义，覆盖不全返回 `Unknown` 不猜。
- 移动抽象：`IMovementScene` 空间载体契约 + `BattlefieldLayout` 布局数据 + `RangeShape` 范围判定 + `VectorMath`。

