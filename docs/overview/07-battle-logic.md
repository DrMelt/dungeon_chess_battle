# DungeonChessBattle.Battle.Logic

战斗逻辑实现层，所属分组 Shared。以 `BattleScene` 实现战场查询视图 `IBattleSceneView` 并编排战斗节拍，以输入门面 `BattleIntentHub` 收拢宿主的全部意图提交，提供 Buff、仇恨、移动与战斗结算的具体逻辑，只面向领域类型，不依赖网络。职责边界见 `functional_boundary/07`。

## 战斗世界

- `BattleScene` 实现 `IBattleSceneView`：`AddUnit` 注册领域单位 `BattleUnit`；单位权威状态自持，阶段由宿主写 `CurrentPhase`，移动在 `Tick` 内统一结算，状态同步由外部 `BattleStateSynchronizer` 完成，场景只做推进与结算。
- 意图写入口一律 `internal`（`SubmitMove`、`ApplyDecisions`）或收在单位字段的 setter（`MoveInput`、`CastInput`），宿主只能经输入门面提交，见「输入门面」。
- 施法裁定只有一个消费点：`Tick` 的读条推进段，`Tick` 之前谁先提交意图不影响裁定结果。
- 瞬发技能（SpellTime=0）校验通过即立即结算，不进入读条状态机，故无读条可被打断。

## 输入门面

`BattleIntentHub` 是宿主（战斗房间、回放引擎）唯一的输入面：

- 对外四个动作：`PrepareTick`（AI 决策 → 在架施法重试）、`SubmitMove`、`SubmitCast`、`ClearQueuedCasts`。排队器 `CastPreInputBuffer` 由门面私有持有（类型亦 `internal`），宿主取不到它。
- 键一律为网络 ID，施法者与目标在门内经 `BattleScene.FindBattleUnit` 解析，解析不到即不接管，服务端与回放同判。
- 战斗推进不经门面：`AddUnit`/`RemoveUnit`/`CurrentPhase`/`Tick` 由宿主直接驱动 `BattleScene`。该写面的授权构成见 `overview/06`。

## 帧节拍

- 战斗循环经 LES `BattleLoop`（LocalSingleton）收编，一个逻辑 tick 内输入落点固定四段：`BattleLoop.Update`（门面 `PrepareTick`：AI 决策 → 在架施法重试）→ `OnLogicTick` 读客户端可靠请求（施法、聚焦）→ 实体 `Update` 转发玩家移动输入 → `BattleLoop.LateUpdate`（`Tick` 推进 → 状态同步 → 整帧事件外送）。与实体同步严格 1:1，时间由 LES accumulator 管理。
- 各落点只登记意图、不就地裁定，落点先后不参与判定。仍需同序的只剩门面内的在架重试先于本帧新按键：同一单位后写覆盖先写。
- `ApplyDecisions`（仅 `PrepareTick` 可调）只在 Running 阶段逐单位触发 `IUnitIntelligence.Decide`，决策产出的是意图本身（`MoveInput`、`CastInput`），不触碰结算状态。

## Tick 推进管线

`Tick(deltaTime)` 单出口，仅 Running 推进第 1–7 步，第 8 步无条件执行：

1. 清空帧事件日志；累加运行时长，结算 Buff 全局节拍的跳数。
2. 位移解算：读存活单位本帧移动输入组装为 `MoveIntent`，交 `IMovementScene.Resolve`，结果回写位置与朝向。不清意图——下一段还要据其判打断。
3. 逐单位读条推进段：施法意图的唯一消费点，见「施法意图消费」。
4. 逐单位推进全局与个体冷却、Buff（Buff 按 3 秒全局节拍同时结算一跳）。
5. 战斗结束判定切 Finished。
6. 仇恨分发：`HateDispatcher` 把帧事件流交给每个存活单位按自身规则求值 → 效果按持有者路由落账。
7. 死者仇恨账本清理，置于推衍之后避免死者被自身伤害事件重写。
8. 作废本帧两类意图：`MoveInput` 归零、`CastInput` 置空。

施法裁定全在第 3 步，事件直写本帧日志，参与当帧仇恨分发与状态同步。射程判定与结算读的都是第 2 步解算后的位置，同一份读数；技能写入的冷却仍在同帧第 4 步被扣一次，写在前扣在后，是既定行为。剩余偏差在 AI：其 `Decide` 发生在 `Tick` 之前，读上一 tick 末位置，与第 3 步的裁定之间隔着一次位移解算。

单位死亡不产出事件：死亡是生命值派生的状态，消费方一律经 `IsDead` 判定，服务端聚焦清活与客户端视图隐藏同源消费；施法目标校验是已知例外，见「施法意图消费」。

## 施法意图消费

裁定集中在 `Tick` 的读条推进段，逐单位三步，顺序即优先级：

- 消费 `CastInput`：技能属该单位且 `SkillCastValidator.CanCast` 通过后，瞬发立即结算、否则写入读条状态与目标。未通过只记日志不改状态，意图不退回——重投由输入源负责。
- 打断判定：本帧有非零位移意图且仍在读条则取消并产 `CastCanceled`。消费排在这一步之前，故同 tick 内「起读条 + 位移」当帧即被打断。
- 推进读条：扣完即结算并清理读条状态。
- 来源无差别：玩家按键（含排队到期转投）与 AI 决策写的都是同一个 `CastInput`。
- 目标存活不设校验，意图不会因目标死亡而失效。校验、目标阵营判定与效果层三处都不看存活，生命写回亦只钳区间不判下界，于是对 `Health == 0` 的单位施放治疗或挂 HoT 会把它抬回存活，而其仇恨账本已被 `CleanupDeaths` 清空、`TryEndBattle` 可能已判过结束。施法者侧有 `IsDead` 防御，目标侧没有。预输入窗口把这条窄竞态的触发面放大到一个排队周期。

## 施法预输入缓冲

`CastPreInputBuffer`（`internal`）由输入门面私有持有，把状态未就绪的按键推迟到就绪的 tick，再转投为该单位的 `CastInput`。排队状态不进同步通道、不参与结算。

- 唯一重试判据是 `SkillCastValidator.IsStateReady`（存活、非读条、总冷却归零）。射程、阵营与技能归属一律不预判，就绪时转投一次，被拒即弃。
- 单槽语义：同一施法者只保一条，新按键覆盖旧意图并满窗重计。目标持 `BattleUnit` 引用，与读条目标同源，不做 ID 重解析。
- 窗口是域内常量 `WindowSeconds = 0.5f`，服务端与回放必须同值，不开放配置注入；改值须一并决定既有录像的去留。当前取值跨不过 2.5 秒 GCD 与 2.0 秒读条，CD 期间的按键在窗口内等不到就绪即作废。
- `Submit` 无返回值：两条分支都是接管（转投为本帧意图或入槽），不含可施放性结论。唯一的失败信号在门面 `SubmitCast`：施法者或目标解析不到。
- 在架意图持领域单位引用，故回放重建单位后必须经门面 `ClearQueuedCasts()`，否则转投落在已被替换的旧对象上。

## 事件日志

- `BattleEventLog` 每帧开头 Clear → 只增追加 → 帧末经只读视图外送，日志仅当帧有效，调用方不得跨帧持有。非 Running 阶段不推进，返回的就是这份空日志。

## 确定性移动

- `MovementMath` 纯函数层：位移增量、两两互斥让位、圆↔矩形推挤、边界钳制。`IMovementScene.Resolve` 收整帧全部意图一次批量解算，服务端、在线与回放经 `BattleScene.Tick` 共用同一路径。
- 互斥范围限于本帧有位移意图的存活单位：静止与死亡单位不入意图集，既不被推开也不构成他人障碍；让位为就地迭代，结果依赖意图顺序，故三端必须同序组装意图。
- `PhysicsMovementScene` 基于 Aether.Physics2D 只做静态几何宽相查询，不运行动态模拟，天然适配 LES 回滚重放；固定子步长防快速单位隧穿细薄障碍，位移途中不再做单位间检测。
- 三条输入路径都逐 tick 重投，`Tick` 末作废才成立：服务端 LES 每 tick 转发 `CurrentInput`（无新输入包时是末值）、AI 每 tick 重决策、回放逐帧注入。转发一停（控制器或载体销毁）下一 tick 即静止，服务端解绑无需显式归零；玩家卡发送不在此列——LES 仍每 tick 转发末值、位移照旧，时效缺在转发层，见 `flow/client-prediction` 的 D11。
- 持续性位移（击退、冲刺）不得建成意图：它无源可投，只能是领域状态由 `Tick` 递进。

## 结算纯函数

- `CastResolver` / `DamageProcessor` / `HealProcessor` 只做数值计算与范围判定，状态写回由 BattleScene 依据返回值完成；`BuffFactory` 把只读定义转换为运行时实例与效果策略。

