# DungeonChessBattle.Battle.Logic

战斗逻辑实现层，所属分组 Shared。以 `BattleScene` 实现战场查询视图 `IBattleSceneView` 并编排战斗节拍，提供 Buff、仇恨、移动与战斗结算的具体逻辑，只面向领域类型，不依赖网络。职责边界见 `functional_boundary/07`。

## 战斗世界

- `BattleScene` 实现 `IBattleSceneView`：`AddUnit` 注册领域单位 `BattleUnit`；单位权威状态自持，阶段由宿主写 `CurrentPhase`，移动在 `Tick` 内统一结算，状态同步由外部 `BattleStateSynchronizer` 完成，场景只做推进与结算。
- 瞬发技能（SpellTime=0）校验通过即立即结算，不进入读条状态机，不受移动打断影响。

## 帧节拍

- 战斗循环经 LES `BattleLoop`（LocalSingleton）收编：`Update` = `ApplyDecisions`（AI 决策前置，输入本帧生效）；`LateUpdate` = `Tick`（推进）→ 状态同步 → 整帧事件外送。与实体同步严格 1:1，时间由 LES accumulator 管理。
- `ApplyDecisions` 仅 Running 阶段逐单位触发 `IUnitIntelligence.Decide`，决策动作在战斗世界内统一执行（移动输入、施法请求）。

## Tick 推进管线

`Tick(deltaTime)` 仅在 Running 推进，依次：

1. 累加运行时长与 Buff 全局节拍，清空帧事件日志并汇入 `_pendingEvents` 跨帧缓冲（Tick 之外的施法开始/取消事件在此）。
2. 移动批量结算：存活且有位移输入的单位组装为 `MoveIntent`，交 `IMovementScene.Resolve` 解算，结果按意图回写位置与朝向。
3. 逐单位推进读条、全局与个体冷却、Buff（Buff 按 3 秒全局节拍同时结算一跳）。
4. 战斗结束判定切 Finished。
5. 仇恨分发：`HateDispatcher` 把帧事件流交给每个存活单位按自身规则求值 → 效果按持有者路由落账。
6. 死者仇恨账本清理，置于推衍之后避免死者被自身伤害事件重写。

单位死亡不产出事件：死亡是生命值派生的状态，一律经 `IsDead` 判定，服务端聚焦清活与客户端视图隐藏同源消费。

## 事件日志

- `BattleEventLog` 每帧 Clear → Append → 帧末经只读视图外送，日志仅当帧有效，调用方不得跨帧持有。非 Running 阶段缓冲一并清空不外送。

## 确定性移动

- `MovementMath` 纯函数层：位移增量、两两互斥让位、圆↔矩形推挤、边界钳制。`IMovementScene.Resolve` 收整帧全部意图一次批量解算，服务端、在线与回放经 `BattleScene.Tick` 共用同一路径。
- 互斥范围限于本帧有位移输入的存活单位：静止与死亡单位不入意图集，既不被推开也不构成他人障碍；让位为就地迭代，结果依赖意图顺序，故三端必须同序组装意图。
- `PhysicsMovementScene` 基于 Aether.Physics2D 只做静态几何宽相查询，不运行动态模拟，天然适配 LES 回滚重放；固定子步长防快速单位隧穿细薄障碍，位移途中不再做单位间检测。

## 结算纯函数

- `CastResolver` / `DamageProcessor` / `HealProcessor` 只做数值计算与范围判定，状态写回由 BattleScene 依据返回值完成；`BuffFactory` 把只读定义转换为运行时实例与效果策略。

