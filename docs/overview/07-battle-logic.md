# DungeonChessBattle.Battle.Logic

战斗逻辑实现层，所属分组 Shared。以 `BattleScene` 实现战场查询视图 `IBattleSceneView` 并编排战斗节拍，以平级的 `CastPreInputBuffer` 承担玩家按键的施法预输入排队，提供 Buff、仇恨、移动与战斗结算的具体逻辑，只面向领域类型，不依赖网络。职责边界见 `functional_boundary/07`。

## 战斗世界

- `BattleScene` 实现 `IBattleSceneView`：`AddUnit` 注册领域单位 `BattleUnit`；单位权威状态自持，阶段由宿主写 `CurrentPhase`，移动在 `Tick` 内统一结算，状态同步由外部 `BattleStateSynchronizer` 完成，场景只做推进与结算。
- 瞬发技能（SpellTime=0）校验通过即立即结算，不进入读条状态机，不受移动打断影响。

## 帧节拍

- 战斗循环经 LES `BattleLoop`（LocalSingleton）收编：`Update` = `ApplyDecisions`（AI 决策前置）→ `CastPreInputBuffer.Advance`（预输入重试）；`LateUpdate` = `Tick`（推进）→ 状态同步 → 整帧事件外送。与实体同步严格 1:1，时间由 LES accumulator 管理。
- `ApplyDecisions` 仅 Running 阶段逐单位触发 `IUnitIntelligence.Decide`，决策动作在战斗世界内统一执行（移动输入、施法请求）。

## Tick 推进管线

`Tick(deltaTime)` 仅在 Running 推进，依次：

1. 累加运行时长与 Buff 全局节拍，清空帧事件日志并汇入 `_pendingEvents` 跨帧缓冲（Tick 之外的施法与预输入落地事件在此）。
2. 移动批量结算：存活且有位移输入的单位组装为 `MoveIntent`，交 `IMovementScene.Resolve` 解算，结果按意图回写位置与朝向。
3. 逐单位推进读条、全局与个体冷却、Buff（Buff 按 3 秒全局节拍同时结算一跳）。
4. 战斗结束判定切 Finished。
5. 仇恨分发：`HateDispatcher` 把帧事件流交给每个存活单位按自身规则求值 → 效果按持有者路由落账。
6. 死者仇恨账本清理，置于推衍之后避免死者被自身伤害事件重写。

一切 Tick 外的施法都发生在 `Tick` 之前的同一输入窗口：AI 决策、玩家按键与预输入重试产出的事件在当帧 `Tick` 开头汇入帧日志，参与当帧仇恨分发与状态同步；其读值是上一 tick 末尾的位置与冷却，瞬发写入的冷却在同帧 `TickCooldowns` 被扣一次。`Tick` 内不含任何输入重议阶段。

单位死亡不产出事件：死亡是生命值派生的状态，消费方一律经 `IsDead` 判定，服务端聚焦清活与客户端视图隐藏同源消费；施法目标校验是已知例外，见「施法入口」。

## 施法入口

- `BattleScene.TryCast` 是唯一对外施法入口，只能在 `Tick` 之外调用：技能属该单位且 `SkillCastValidator.CanCast` 通过后，瞬发立即结算、否则写读条状态与目标。未落地不改状态不产事件，也不回报失败原因——调用方只拿 bool，要不要重试由调用方自己的策略决定。
- 两个调用方：场景内 AI 的 `RequestCast`（每 tick 重决策自带重试）与 `CastPreInputBuffer`（玩家按键排队）。
- 事件日志归属由落地点决定：Tick 外用 `_scratchLog` 暂存后汇入 `_pendingEvents`（Tick 外入口不可重入，故复用不分配），Tick 内读条完成直写本帧 `_eventLog`。
- 目标存活不设校验，意图不会因目标死亡而失效。校验、目标阵营判定与效果层三处都不看存活，生命写回亦只钳区间不判下界，于是对 `Health == 0` 的单位施放治疗或挂 HoT 会把它抬回存活，而其仇恨账本已被 `CleanupDeaths` 清空、`TryEndBattle` 可能已判过结束。施法者侧有 `IsDead` 防御，目标侧没有。预输入窗口把这条窄竞态的触发面放大到一个排队周期。

## 施法预输入缓冲

`CastPreInputBuffer` 与 `BattleScene` 平级，由权威宿主持有并驱动，把玩家一次按键推迟到状态就绪的 tick 再交回战斗世界裁定。排队状态是宿主待办：不写进领域单位、不进同步通道、不进 `Tick` 阶段序——战斗世界不感知预输入。

- 唯一重试判据是 `SkillCastValidator.IsStateReady`（存活、非读条、总冷却归零）。会自然转就绪的状态阻塞才值得等待；射程、阵营与技能归属一律不预判，就绪时提交一次，被拒即弃。
- 单槽语义：同一施法者只保一条，新按键覆盖旧意图并满窗重计。目标持 `BattleUnit` 引用，与读条目标同源，不做 ID 重解析。移动中落地的读条照旧被后续位移输入打断。
- 窗口是域内常量 `WindowSeconds = 0.5f`，服务端与回放同源，不开放配置注入：注入即让两端取到不同值，新录像静默偏离权威实况。改值属战斗内容变更，须一并决定既有录像的去留。当前取值跨不过 2.5 秒 GCD 与 2.0 秒读条，CD 期间的按键在窗口内等不到就绪即作废；调窗是策略取值问题，不涉及机制。
- `Submit` 返回 true 只表示意图已被接管（已提交或已入槽），不保证最终可施放：按下一个不属于该单位的技能键，若正落在 GCD 内同样拿到 true，就绪后才由战斗世界拒绝。
- `Advance(dt)` 必须在 `Tick` 之前被驱动，顺序固定为 AI 决策 → 预输入重试 → 本帧新输入 → `Tick`。服务端可靠请求在 LES `OnLogicTick` 内到达、即钩子 `Update` 之后，回放按同序注入才能复现同一落地时刻。
- 意图不挂在单位上，故回放 `Reset()` 重建单位后必须显式 `Clear()`，否则在架意图会落到已被替换的旧单位引用上。

## 事件日志

- `BattleEventLog` 每帧 Clear → Append → 帧末经只读视图外送，日志仅当帧有效，调用方不得跨帧持有。非 Running 阶段缓冲一并清空不外送。

## 确定性移动

- `MovementMath` 纯函数层：位移增量、两两互斥让位、圆↔矩形推挤、边界钳制。`IMovementScene.Resolve` 收整帧全部意图一次批量解算，服务端、在线与回放经 `BattleScene.Tick` 共用同一路径。
- 互斥范围限于本帧有位移输入的存活单位：静止与死亡单位不入意图集，既不被推开也不构成他人障碍；让位为就地迭代，结果依赖意图顺序，故三端必须同序组装意图。
- `PhysicsMovementScene` 基于 Aether.Physics2D 只做静态几何宽相查询，不运行动态模拟，天然适配 LES 回滚重放；固定子步长防快速单位隧穿细薄障碍，位移途中不再做单位间检测。
- 移动输入是写入方维持的状态，`Tick` 只消费不清零：输入源消失（控制器解绑、客户端停发）即按末值持续位移，归零责任在写入方，服务端断开路径已显式补这一步。

## 结算纯函数

- `CastResolver` / `DamageProcessor` / `HealProcessor` 只做数值计算与范围判定，状态写回由 BattleScene 依据返回值完成；`BuffFactory` 把只读定义转换为运行时实例与效果策略。

