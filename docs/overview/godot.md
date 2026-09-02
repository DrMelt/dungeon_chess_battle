# Godot 主工程内部机制

覆盖 `DungeonChessBattle.Game`：装配、驱动、战斗进出、单位视图与全部界面。跨进程握手与重连时序见 `flow/connection-reconnect`；模块边界见 `functional_boundary/01`。

## 装配根与子进程

- `ServiceLocator` 是静态组合根，全程无 DI 容器：创建 `ILoggerFactory`（`GodotLoggerProvider` 接入 Godot 控制台）、安装 LES 框架日志转接、装配 `GameClientService`、`ServerProcessHost` 与回放浏览服务，静态字段即组合根。
- `ResourceTables` 是展示资源表组合根：技能/Buff/副本三表唯一加载入口与唯一 `res://` 路径持有者，面板与表现层经静态属性取表，不触碰路径字符串。
- 服务器是独立子进程：`ServerProcessHost` 解析服务器可执行路径，以 `--port` 传端口、环境变量 `DCB_SERVER_PASSWORD` 传密码、`DCB_SERVER_PARENT_PID` 传父 PID，就绪经 TCP 端口探测判定（握手时序见 `flow/connection-reconnect`）。
- 子进程状态为查询式：后台线程只更新加锁保护的内部字段，UI 主线程轮询 `Status`，从根上避免跨线程触碰 Godot 节点。

## 帧驱动

- `BattleCoordinator._Process` 最先执行（`main_scene.tscn` 设 `process_priority = -1`）：由 `BattleInputController` 采集 WASD 移动与 3D 目标拾取，每帧提交到 `IClientBattleService`。输入先于网络驱动，本帧 pending 在紧随的逻辑 tick 内即被采纳；代价是同一 `Tick` 里的 `UpdateRaycast` 取上一帧相机位，见 `flow/client-prediction` 的 D8。完整一帧次序以 `flow/client-prediction` 的时序表为准，此处不复述。
- `GameClientDriver._Process` 随后：`ClientService.Update` 消费主线程动作队列、驱动两端网络轮询并监测连接超时。

## 战斗进出与屏幕态

- `MainScene` 订阅服务层事实源事件：`OnBattleStarted` 进入战斗、`OnBattleSessionLost`（重连失败/完全断开）退出战斗、`OnBattleFinished`（Finished 阶段）走应用级退出。
- `BattleCoordinator.EnterBattle` 统一绑定 `UnitShowManager`、`BattleSessionContext`、`BattleInputController` 并订阅战斗阶段事件，`ExitBattle` 反向解绑；重连恢复时先退出旧绑定再重入。
- 阶段事件经 `CallDeferred` 转到下一帧处理，保证房间实体同步已完成。
- `ScreenStateMachine` 只仲裁 FrontUI 与在线战斗 UI 容器显隐，经 `ReplayStarted`/`ReplayFinished` 等信号进 `Replay` 态；它不认识任何具体面板。

## 取数与视图

- UI 不直接持有网络对象：会话入口是门面的 `RoomSession`（`IClientBattleSession`），事件经其 C# 事件到达，数据查询统一经 `BattleSessionContext` 投影（本地 Pawn、全部单位、副本键、战斗计时、阵营关系函数）。诊断面板读门面的 `RoomNetworkStatus` 快照。
- `UnitShowManager` 是单位视图唯一所有者：每帧从 `IBattleViewSource` 增量生成视图、重取单位引用并按 `IsDead` 收敛可见性；在线与回放经 `Bind` 切数据源，共用同一 `IBattleViewSource`，`UnitGameShow` 零改动；技能展示资源由 UI 侧按技能 ID 经 `ResourceTables.Skills` 直查，范围提示与施放特效场景模板挂在技能资源上，由 `EffectHints` 创建与回收；不在此装配、不对外提供查询。
- 副本环境经资源工厂创建：场景模板挂在副本资源 `EnvScene`，`DungeonResourceTable.InstantiateEnvironment` 按副本键实例化（未同步键回退默认副本模板），`BattleCoordinator` 管理创建与销毁并据会话副本键应用主题。
- 阵营判定依赖 `DungeonRegistry.GetRelations(dungeonKey)` 装配的关系函数，副本键同步后延迟收敛，未知键抛异常不静默回退。

## 回放表现归属

- `Game/Replay/` 一场景一目录、一所有者：`ReplayPanel` 取数与呈现、不碰屏幕态，`ReplayItem` 暴露下载与播放两按钮并上报房间 ID，`ReplayHud` 只管播放控制（默认隐藏），`ReplayCoordinator` 管引擎生命周期与表现绑定。
- 过程状态在 `ServiceLocator.ReplayService`（缓存、双重门控、服务端 ∪ 本地并集裁决），面板 `_Process` 每帧读行视图渲染；下载进度、缓存命中与版本不符都表现为行状态。
- 启动回放仅由 `ReplayPanel` 对播放按钮显式触发，后台获取完成不自动进入。入口面板是前厅页面之一，由 GameLobby 经 `BaseGamePanel` 导航链打开，启动播放后自行返回，故退出回放落回大厅——落点归导航链，显隐归 `ScreenStateMachine`。
- 事件反馈复用在线 `UnitStateChangeInfo`：`ReplayCoordinator` 每帧把 `ReplayEngine.Step()` 的 `IBattleEvent` 流喂给它，弹共用的受击/治疗/Buff 浮字，退出解绑。
