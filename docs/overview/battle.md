# 战斗域内部机制

覆盖 `Battle.Shared`、`Battle.Logic`、`Battle.Entities`、`Battle.Server` 与 `GameConfig`。端到端下行链见 `flow/battle-state-sync`，跨模块时序不在此复述；模块边界见 `functional_boundary/06`、`07`、`08`、`13`、`09`。

## Tick 推进管线

`BattleScene.Tick(deltaTime)` 单出口，仅 Running 推进 1–8 步，第 9 步无条件执行：

1. 清空帧事件日志；累加运行时长，结算 Buff 全局节拍跳数。
2. 位移解算：读存活单位本帧移动输入组装 `MoveIntent`，交 `IMovementScene.Resolve`，结果回写位置与朝向。不清意图——下一段还要据其判打断。
3. 逐单位读条推进段：施法意图的唯一消费点。
4. 逐单位推进全局冷却、个体冷却与 Buff（Buff 按 3 秒全局节拍同时结算一跳）。
5. 战斗结束判定 `TryEndBattle` 切 Finished。
6. 仇恨分发：`HateDispatcher` 把帧事件流交给每个存活单位按自身规则求值，效果按持有者路由落账。
7. 死者仇恨账本清理 `CleanupDeaths`，置于推衍之后，避免死者被自身伤害事件重写。
8. 聚焦清活：`FocusTarget` 指向不存在或已死亡单位时归零，判据与门面设置期校验同源。
9. 作废本帧两类意图：`MoveInput` 归零、`CastInput` 置空。聚焦是持续态，不在此列。

- 裁定全在第 3 步：瞬发（SpellTime=0）校验通过即立即结算、不进读条状态机，故无读条可被打断；打断判定排在消费之后，同 tick 内「起读条 + 位移」当帧即被打断；未通过只记日志不改状态，意图不退回，重投由输入源负责。
- 射程判定与结算读的都是第 2 步解算后的位置，同一份读数；技能写入的冷却仍在同帧第 4 步被扣一次，写在前扣在后，是既定行为。剩余偏差在 AI：`Decide` 发生在 `Tick` 之前，读上一 tick 末位置，与第 3 步裁定之间隔着一次位移解算。
- 单位死亡不产出事件：死亡是生命值派生态，消费方一律经 `IsDead` 判定。施法目标校验是已知例外——校验、目标阵营判定与效果层三处都不看存活，生命写回只钳区间不判下界，对 `Health == 0` 的单位施放治疗或挂 HoT 会把它抬回存活，而其仇恨账本已被 `CleanupDeaths` 清空、`TryEndBattle` 可能已判过结束。施法者侧有 `IsDead` 防御，目标侧没有；预输入窗口把这条窄竞态的触发面放大到一个排队周期。
- `BattleEventLog` 每帧开头 Clear → 只增追加 → 帧末经只读视图外送，仅当帧有效，调用方不得跨帧持有。非 Running 阶段不推进，返回的就是这份空日志。
- 这份步序即 `BattleLogicRevision.Value` 所背的东西：步骤顺序、事件产生次序、第 9 步作废口径任一变动都要递增，否则旧录像在同一份输入下重跑出不同结果而双重门控看不出差别。漏递增不报错。内容与布局侧的变化走 `GameConfigDB.DataRevision`，不走这里。

## 输入门面与预输入

`BattleIntentHub` 是宿主（战斗房间、回放引擎）唯一的输入面：

- 对外三个动作：`PrepareTick`（AI 决策 → 在架施法重试）、`Submit(PlayerCommand)`、`ClearQueuedCasts`。载荷合法性（阶段、技能键、ID 解析、聚焦目标存活）只判一次，服务端与回放同判。
- 生命周期两类：移动与施法是本帧意图，随 `Tick` 末作废、由输入源逐 tick 重投；聚焦是持续状态，设定后保持，只随目标死亡清零。命令统一的是提交路径，不是生命周期。
- 键一律为 `UnitId`，施法者与目标在门内经 `BattleScene.FindBattleUnit` 解析，解析不到即不接管；「无目标」的门内判据是 `UnitId.IsDefault`，与边界侧的裸 0 同义。施法者与聚焦持有者一律由服务端从请求来源控制器推导，客户端不得指定。
- 战斗推进不经门面：`AddUnit`/`RemoveUnit`/`CurrentPhase`/`Tick` 由宿主直接驱动 `BattleScene`。该写面的授权构成见下条。
- 写权限边界：本层 `internal` 成员经 `InternalsVisibleTo` 只授 Battle.Logic，构成「战斗世界可写、其余程序集不可写」的输入写面，当前是 `BattleUnit.MoveInput` 与 `BattleUnit.CastInput` 两个 setter；`FocusTarget` 不在该面内，它与生命值同规格，服务端由门面写、在线端由同步通道回填。
- `CastPreInputBuffer`（`internal`）由门面私有持有，把状态未就绪的按键推迟到就绪的 tick 再转投为该单位 `CastInput`；排队状态不进同步通道、不参与结算。唯一重试判据是 `SkillCastValidator.IsStateReady`（存活、非读条、总冷却归零），射程、阵营与技能归属一律不预判，就绪时转投一次、被拒即弃。
- 单槽语义：同一施法者只保一条，新按键覆盖旧意图并满窗重计；目标持 `BattleUnit` 引用，与读条目标同源，不做 ID 重解析。`Submit` 无返回值，两条分支都是接管；权威结论由门面 `Submit` 给出，false 的成因都在门内。
- 窗口是域内常量 `WindowSeconds = 0.5f`，服务端与回放必须同值、不开放配置注入，改值须一并决定既有录像的去留。当前取值跨不过 2.5 秒 GCD 与 2.0 秒读条，CD 期间的按键在窗口内等不到就绪即作废。
- 在架意图持领域单位引用，故回放重建单位后必须经门面 `ClearQueuedCasts()`，否则转投落在已被替换的旧对象上。

## 确定性移动

- `MovementMath` 纯函数层：位移增量、两两互斥让位、圆↔矩形推挤、边界钳制。`IMovementScene.Resolve` 收整帧全部意图一次批量解算，服务端、在线与回放经 `BattleScene.Tick` 共用同一路径。
- 让位为就地迭代、结果依赖意图顺序，故三端必须同序组装意图；互斥范围限于本帧有位移意图的存活单位，静止与死亡单位不入意图集，既不被推开也不构成他人障碍。
- `PhysicsMovementScene` 基于 Aether.Physics2D 只做静态几何宽相查询，不运行动态模拟，天然适配 LES 回滚重放；固定子步长防快速单位隧穿细薄障碍，位移途中不再做单位间检测。
- 三条输入路径都逐 tick 重投，`Tick` 末作废才成立：服务端 LES 每 tick 转发 `CurrentInput`（无新输入包时是末值）、AI 每 tick 重决策、回放逐帧注入。转发一停（控制器或载体销毁）下一 tick 即静止，服务端解绑无需显式归零；玩家卡发送不在此列，时效缺在转发层，见 `flow/client-prediction` 的 D11。
- 持续性位移（击退、冲刺）不得建成移动意图：它无源可投，只能是领域状态由 `Tick` 递进。

## 视图契约与 ID 口径

- `IUnitCombatView` 是公共面（身份 + 数值 + 技能源）：`ISkillCasterView` 在其上加 `IWorldPoseView`（碰撞半径 + 逻辑位置），是 `SkillCastValidator` 依赖的施法判定最小子集；`IUnitUiView` 在其上加展示位置，是 UI 唯一取数口径。`IBattleUnitView : ISkillCasterView, ICombatStatsView, IHateActorView` 是领域只读（服务端 / AI / 仇恨），`IBuffUiView` 由 `ActiveBuff` 实现。
- `Position` 与判定共用同一份读数（在线为下行回填值，回放为本地结算值），不再分离插值与权威两份。
- `ISkillCasterView` 无在线消费者：可否施放含预输入排队一律由权威裁定，在线端只暴露展示视图。
- 单位 ID 在领域、命令与仇恨账本内一律 `UnitId`（成员名 `SourceUnitId`/`TargetUnitId`/`UnitId`），0 恒非法即 `UnitId.None`（LES 同步实体 ID 从 1 起分配）。SyncVar 与 MessagePack 不认包装类型，线协议、同步实体与回放条目恒为原生 `ushort`，这类字段一律用 `…NetId` 命名——后缀即「此处是原生承载，未进强类型」。领域内部不再有裸 ID 中转，收放只发生在命令构造点（服务端请求转发、`ReplayCommands`）与同步编解码点（`BattleEventCoder`、`UnitPawn.StateSync`）。

## 同步通道与搬运规则

`UnitPawn.StateSync.cs` 是 `BattleUnit` ↔ `UnitPawn` 字段清单的唯一声明处，`SyncFrom`（服务端投影，领域 → 载体）与 `SyncInto`（在线回填，载体 → 领域）逐字段成对，不设端别守卫，选向由调用点负责；调用点只做配对与调度，不出现字段清单。搬运规则六条：

- 计数型字段（生命、位置、半径、读条剩余等）直接写 SyncVar，靠 LES 做增量 diff。
- 冷却 / Buff / 仇恨 `SyncList`：服务端逐字段比对内容、一致则跳过重建，避免每帧全量发送；在线端按下行列表的内容指纹比对，指纹未变只跳过领域列表重建，条目剩余秒仍逐帧原地刷新。指纹归属回填的领域单位，换绑（含 LES 实体池复用）即失效，无需调用方重置。
- 倒计时字段写**截止 tick**（`EndServerTick`），不逐 tick 推当前值；回填侧按本端插值 `ServerTick` 经 `SyncTickHelper.RemainingSeconds` 反算剩余秒（`SequenceDiff` 处理 16 位回绕），换算只出现在通道内。剩余秒非正一律落哨兵 0，反算见 0 短路归零、不参与 tick 差值——写成当前 tick 等于每 tick 重定基，两端 tick 同步前进，反算出的差永不收敛。
- `MaxStacks`、`StackCount`、`DamageType` 等 Buff 字段随 Buff 条目一起写；在线端还原为 `ActiveBuff` 展示壳（占位定义 `NetworkBuffDefinition` + 永不触发的 `NoOpBuffEffect`），不推进效果。
- 仇恨表只下行不回填：在线端不跑仇恨结算与 AI。聚焦随通道双向——服务端投影领域值，在线端回填后由 UI 直读，本地不推算。
- 每个剩余秒字段都要有推进者，且只在 `BattleScene.Tick` 内推进：读条 `SkillCastRemaining`、全局冷却 `GcdRemaining`、个体冷却 `CooldownEntry.Remaining`、`BuffInstance.Remaining` 各一处。截止 tick 是源剩余秒的派生量，源不推进则派生量逐 tick 重定基，本端读到一个恒定正数：显示上时间永不动，判定上冷却永不到期。

字段清单曾分散在服务端投影器与客户端镜像两处，下行有值而领域无读数的缺口即由此产生；通道收拢后同类缺口换了形态——字段搬进了领域，推进者没跟着搬，见末条。

## LES 使用约束

- `EntityTypesRegistry.EntityTypesMap` 的注册顺序服务端与客户端必须完全一致，静态构造注册自定义字段类型 `Vector2`（含插值器）。LES 对未注册字段类型静默剔除——缺失注册的表现是字段不参与同步，没有报错。
- `BattleRoomEntity` 禁止在 `OnConstructed` 重置同步字段：客户端先应用初始同步再执行 `OnConstructed`，重置会丢字段。
- `UnitPawn.Update` 仅 `base.Update()`：纯投影载体，位移由领域 `BattleScene` 统一结算。技能定义、智能决策器、移动管线为装配期本地写入，不参与同步。
- 输入包 `UnitInputPacket` 为扁平顺序布局结构，只承载移动状态；施法、聚焦等一次性事件走可靠请求通道。
- 战斗事件编解码 `BattleEventCoder` 是领域事件 ↔ `SyncBattleEvent` 的双向映射唯一权威（tag 与槽位语义在此），解码遇未知 tag 返回 null 向前兼容。可靠消息帧 `ReliableMessageFrame` + `ReliableBattleEventLog` 消息体由服务端外送与客户端解析共用。

## 房间线程模型与生命周期

- 线程所有权严格：EntityManager 的全部操作（初始化、建实体、RPC、`Update`）只在房间线程；大厅线程只做生命周期控制（启动、等待初始化信号、停止）。每个战斗房间 = 独立后台线程 + 独立 `NetManager` + 独立 `ServerEntityManager` + 独立 `BattleScene` + 独立端口（50 FPS，sendRate 与帧率一致），实体同步物理隔离。
- `BattleRoomManager` 管理端口池（从 `FirstRoomPort` 递增分配，销毁回收）与房间注册表，实现 `IBattleRoomManager`，对外只暴露端口等原语。
- 首帧 `InitializeFromStore` 依序：创建 `BattleRoomEntity` 注入权威副本键并装配 `BattleStateSynchronizer` → 从 Store 迁移准备期单位（按副本配置解析玩家阵营、同阵营错开出生点、建 Pawn、注入战斗系数与技能）→ 按副本配置生成敌人（配置键经注册表反查、注入智能决策器）→ 全部单位创建完成后把 NetId、配置键、阵营与出生点整表交录制器落盘 → 注册 `BattleLoop` LocalSingleton 并创建录制器 → `StartBattle` 立即进入 Running。阶段先于客户端连入写定，技能请求不会被阶段校验拒绝。
- 移除 LES 玩家前必须先 `ReleaseControlledPawn` 解绑：`ServerEntityManager.RemovePlayer` 内部走 `DestroyWithControlledEntity`，连带销毁受控 `UnitPawn`。移动输入由 `PawnLogic.Update` 驱动，服务端更新循环跳过已销毁实体——载体一旦销毁，重连绑上的就是死实体且不再报错，输入通道永久失效、全端只见该载体消失。解绑后该单位再无移动输入来源，按帧归零的移动输入使其下一 tick 即静止，无需显式归零；`TryBindPlayerController` 以 `IsDestroyed` 兜底拒绝绑定。
- 断线仅清会话连接状态，单位与投影照常推进下行；连接密钥即 playerId，`OnConnectionRequest` 校验服务器密钥或 Store 房间成员白名单，同一 playerId 已有活跃连接时关旧迎新。重连登记（大厅层 `RegisterPlayer`）仅当房间已有同名会话才允许，杜绝冒用他人 playerId 绑单位。
- `PlayerSession` 聚合 playerId → PeerId/NetPlayer/Controller/Pawn，连接状态是会话本地数据，不产生网络实体。
- 服务端维持「聚焦目标必存活」不变式：死亡不经事件通报，随生命值下行自愈。
- 全部活跃连接断开且初始化完成 → `RoomEmpty` 事件 → 投递队列 → 大厅侧清理循环消费 → `RemoveRoom`（停线程 → 回收端口 → 编码归档回放）。关服 `StopAll` 同样归档，见 `overview/server`。

## 配置登记点

- `UnitRegistry`（ConfigKey ↔ UnitConfig）与 `DungeonRegistry`（DungeonKey ↔ DungeonConfig）是唯一登记点，服务端建模与控制器绑定校验、客户端 `UnitCatalog` 展示共享同一份配置，新增单位/副本必须经登记；敌人生成以注册表权威配置键为准，`GetByConfig` 反查杜绝拼写错配。
- 玩家阵营是选项键 → 实际阵营列表的映射：客户端提交选项键，服务端按 `DungeonConfig.PlayerCampOptions` 权威解析，单位配置不含阵营。
- `DungeonConfig.Layout`（边界 + 静态障碍矩形）是三端构建移动场景的同源依据；`RelationsResolver` 是敌我判定唯一来源。
- `UnitConfig` 除数值外直接装配领域行为（`Intelligence` 无状态实例可多单位多房间共享、`HateRule`、`Skills`），`GameConfigDB` 编译期实例化领域只读定义，零反射、无热加载。
