# Godot 主工程内部机制

覆盖 `DungeonChessBattle.Game`：装配、驱动、战斗进出、单位视图与全部界面。跨进程握手与重连时序见 `flow/connection-reconnect`；模块边界见 `functional_boundary/01`。

## 装配根与子进程

- `ServiceLocator` 是静态组合根，全程无 DI 容器：创建 `ILoggerFactory`（`GodotLoggerProvider` 接入 Godot 控制台）、安装 LES 框架日志转接、装配 `GameClientService`、`ServerProcessHost` 与回放浏览服务，静态字段即组合根。
- 展示资源的 `res://` 路径只在本工程两处出现：`ResourceTables` 持有三张资源表，`BuiltinDisplayAssets` 持有 mod 可引用的引擎预置场景（以资源名登记进展示注册表）。面板与表现层经静态属性取表、经 `ModAssets` 取视图，不碰路径字符串。
- mod 内容装配挂在 `MainScene._EnterTree` 而不是 `_Ready`：Godot 的 `_Ready` 自底向上触发，子面板在自身 `_Ready` 里就开始取数（GameLobby 就在其中填副本下拉），装配挂在主场景 `_Ready` 会晚一步，mod 内容整片看不见。
- 服务器是独立子进程：`ServerProcessHost` 解析服务器可执行路径，以 `--port` 传端口、环境变量 `DCB_SERVER_PASSWORD` 传密码、`DCB_SERVER_PARENT_PID` 传父 PID，就绪经 TCP 端口探测判定（握手时序见 `flow/connection-reconnect`）。
- 子进程状态为查询式：后台线程只更新加锁保护的内部字段，UI 主线程轮询 `Status`，从根上避免跨线程触碰 Godot 节点。

## 帧驱动

- `BattleCoordinator._Process` 最先执行（`battle_assemble.tscn` 设 `process_priority = -1`）：由 `BattleInputController` 采集 WASD 移动与 3D 目标拾取，每帧提交到 `IClientBattleService`。输入先于网络驱动，本帧 pending 在紧随的逻辑 tick 内即被采纳；代价是同一 `Tick` 里的 `UpdateRaycast` 取上一帧相机位，见 `flow/client-prediction` 的 D8。完整一帧次序以 `flow/client-prediction` 的时序表为准，此处不复述。
- `GameClientDriver._Process` 随后：`ClientService.Update` 消费主线程动作队列、驱动两端网络轮询并监测连接超时。

## 场景组装与加载

- 战斗表现拆三棵场景：`battle_world.tscn`（在线与回放共用的战斗世界一棵：`BattleSessionContext`、`BattleInputController`、`UnitShowManager`、相机、`GamePlayEffects`、`BattleGamePlayUI`）、`battle_assemble.tscn`（根即 `BattleCoordinator`，内嵌一份 world）、`replay_assemble.tscn`（根即 `ReplayCoordinator`，自带 `ReplayUI` 回放表现，内嵌一份 world）。
- world 内含唯一相机与单位视图树，两棵组装场景互斥存在：`MainScene` 进入战斗实例化 battle 组装、启动回放实例化 replay 组装，退出即 `QueueFree`，同一时刻至多一套在场。回放组装内 `BattleGamePlayUI` 预设隐藏。
- 新增表现组件进 `battle_world.tscn`，两路自动共用；回放专属面板挂 `replay_assemble.tscn` 的 `ReplayUI` 下，不必动协调器接线。

## 战斗进出与屏幕态

- `MainScene` 订阅服务层事实源事件：`OnBattleStarted` 加载战斗组装并进入战斗、`OnBattleSessionLost`（重连失败/完全断开）退出战斗、`OnBattleFinished`（Finished 阶段）走应用级退出；回放启动由 `ReplayPanel` 交 `MainScene.StartReplay` 装配。
- `BattleCoordinator.EnterBattle` 单独构建在线装配（`OnlineBattleViewSource` + `BattleSessionCommand`）注入 `BattleSessionContext`、重置 `BattleInputController` 并订阅战斗阶段事件，`ExitBattle` 反向解绑；重连恢复时先退出旧绑定再重入。展示组件自持数据源引用，不经编排器逐组件接线。
- 阶段事件经 `CallDeferred` 转到下一帧处理，保证房间实体同步已完成。
- `ScreenStateMachine` 只仲裁 FrontUI 显隐与屏幕态枚举，经 `ReplayStarted`/`ReplayFinished` 等信号进 `Replay` 态；战斗/回放画面的显隐随组装场景加载释放天然成立，它不认识任何具体面板。

## 取数与视图

- 表现层只认一个数据源：`BattleSessionContext` 是纯门面，读数全部转发给当前装配的 `IBattleViewSource`，未绑定时恒空；装配对象由编排器单独构建注入——`OnlineBattleViewSource` 委托门面 `RoomSession`（事件日志也取自会话仓库），`ReplayBattleViewSource` 委托 `ReplayEngine` 并自持回放事件日志仓库（帧事件按引擎帧轴落账）。帧事件缓冲与阵营关系懒装配在 `BattleViewSourceBase` 共用。本节点不认识会话与引擎类型，绑定生命周期只在本节点。UI 与表现组件不持有网络对象与引擎实例，一律以导出节点引用直持本节点（`_sessionRef`）取数，无逐组件 `Bind`。`AppendEvents` 是驱动方投喂一帧领域事件的唯一入口，浮字组件每帧自取。诊断面板读门面的 `RoomNetworkStatus` 快照。
- 命令写侧不进读侧契约：`BattleSessionCommand`（持会话与房间 ID）仅在线装配，经 `BattleSessionContext.Command` 消费（未绑定与回放为 null）；移动输入直接走 `IClientBattleService`。回放不受理命令，本地玩家语义恒 null，本地状态栏与技能面板随之隐藏。
- `UnitShowManager` 是单位视图唯一所有者：每帧从直持的统一数据源增量生成视图、重取单位引用并按 `IsDead` 收敛可见性；本节点不接进出通知，按 `BattleSessionContext.BindGeneration` 自检数据源换向并清场（重连重绑即靠此收敛）；在线与回放各自一份 world 实例，`UnitGameShow` 零改动；技能展示资源由 UI 侧按技能 ID 经 `ResourceTables.Skills` 直查，范围提示与施放特效场景模板挂在技能资源上，由 `EffectHints` 创建与回收；不在此装配、不对外提供查询。
- 副本环境经资源工厂创建：场景模板挂在副本资源 `EnvScene`，`DungeonResourceTable.InstantiateEnvironment` 按副本键实例化（未同步键回退默认副本模板），`BattleCoordinator` 管理创建与销毁并据会话副本键应用主题。
- 阵营判定依赖 `DungeonRegistry.GetRelations(dungeonKey)` 装配的关系函数，副本键同步后延迟收敛，未知键抛异常不静默回退。

## 回放表现归属

- `Game/Replay/` 一场景一目录、一所有者：`ReplayPanel` 取数与呈现、不碰屏幕态，`ReplayItem` 暴露下载与播放两按钮并上报房间 ID，`ReplayHud` 只管播放控制，`ReplayInputPanel` 读引擎输入时间轴呈现当前帧前后条目并提供逐条跳转，`ReplayCoordinator` 管引擎生命周期与表现绑定。
- 回放表现（控制条 `ReplayHud` 与输入面板 `ReplayInputPanel`）挂在 `replay_assemble.tscn` 的 `ReplayUI` 容器下，默认隐藏：整容器显隐是回放表现的唯一开关，由 `ReplayCoordinator` 切；容器与协调器同场景，`_coordinator` 走场景内相对路径。
- 过程状态在 `ServiceLocator.ReplayService`（缓存、双重门控、服务端 ∪ 本地并集裁决），面板 `_Process` 每帧读行视图渲染；下载进度、缓存命中与版本不符都表现为行状态。
- 启动回放仅由 `ReplayPanel` 对播放按钮显式触发，后台获取完成不自动进入；面板取到可重放记录后交 `MainScene.StartReplay` 装配回放场景，成功才返回。入口面板是前厅页面之一，由 GameLobby 经 `BaseGamePanel` 导航链打开，启动播放后自行返回，故退出回放落回大厅——落点归导航链，显隐归 `ScreenStateMachine`，组装归 `MainScene`。
- 事件反馈复用在线 `UnitStateChangeInfo`：在线与回放编排器都把一帧 `IBattleEvent` 交 `BattleSessionContext.AppendEvents`，浮字组件直持数据源、每帧自取帧事件弹受击/治疗/Buff 浮字；日志落账由装配自决——回放装配按帧轴落 `ReplayBattleViewSource` 自持的仓库。
