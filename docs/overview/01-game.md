# DungeonChessBattle.Game（Godot 主工程）

客户端工程，Godot 4.7 C#。场景、UI、战斗表现与网络驱动的最终装配层。不实现任何战斗规则与服务端业务。职责边界见 `functional_boundary/01`。

## 服务装配

- `ServiceLocator` 静态单例：创建 `ILoggerFactory`（GodotLoggerProvider 接入 Godot 控制台），安装 LES 框架日志转接，装配 `GameClientService` 与 `ServerProcessHost`。全程无 DI 容器，静态字段即组合根。
- 服务器是独立子进程：`ServerProcessHost` 解析服务器可执行路径，以 `--port` 传端口、环境变量 `DCB_SERVER_PASSWORD` 传密码、`DCB_SERVER_PARENT_PID` 传父 PID。就绪经 TCP 端口探测判定。
- 子进程状态为查询式：后台线程只更新加锁保护的内部字段，UI 主线程轮询 `Status` 属性，从根上避免跨线程触碰 Godot 节点。

## 帧驱动顺序

1. `BattleCoordinator._Process` 最先执行（`main_scene.tscn` 设 `process_priority = -1`）：由 `BattleInputController` 采集 WASD 移动与 3D 拾取，每帧提交到 `IClientBattleService`。输入先于网络驱动，本帧 pending 在紧随的逻辑 tick 内即被采纳。
2. `GameClientDriver._Process` 随后：`ClientService.Update` 消费主线程动作队列、驱动大厅与房间客户端网络轮询并监测连接超时。

## 战斗进出路由

- `MainScene` 订阅服务层事实源事件：`OnBattleStarted` 进入战斗、`OnBattleSessionLost`（重连失败/完全断开）退出战斗。
- `BattleCoordinator.EnterBattle` 统一绑定 `UnitShowManager`、`BattleSessionContext`、`BattleInputController` 并订阅战斗阶段事件；`ExitBattle` 反向解绑。重连恢复时先退出旧绑定再重入。
- 屏幕状态机 `ScreenStateMachine` 仲裁 FrontUI 容器显隐；战斗 Finished 阶段经 `OnBattleFinished` 回调走应用级退出。
- 阶段事件经 `CallDeferred` 转到下一帧处理，保证房间实体同步已完成。

## 数据流

- UI 不直接持有网络对象：事件经 `IClientBattleService` C# 事件到达，数据查询统一经 `BattleSessionContext` 投影（本地 Pawn、全部单位、副本键、战斗计时、阵营关系函数）。
- `UnitShowManager` 是单位视图唯一所有者：每帧从 `IBattleViewSource` 增量生成视图、重取单位引用并按 `IsDead` 收敛可见性；技能展示资源由 UI 侧按技能 ID 直查 `SkillResourceTable`，不在此装配；不对外提供查询。
- 阵营判定依赖 `DungeonRegistry.GetRelations(dungeonKey)` 装配的关系函数，副本键同步后延迟收敛，未知键抛异常不静默回退。

