# DungeonChessBattle（Godot 主工程）

客户端工程，Godot 4.7 C#。场景、UI、战斗表现与网络驱动的最终装配层。不实现任何战斗规则与服务端业务。

## 职责范围

- 服务装配：`ServiceLocator` 装配 `GameClientService` 与 `ServerProcessHost`，`GameClientDriver` 驱动网络与输入。
- 战斗编排外壳：`MainScene` 订阅战斗启动与会话终结事件、路由战斗进出并仲裁屏幕状态机；`BattleCoordinator` 统一编排战斗子系统生命周期，分发战斗阶段、应用副本环境主题、推进输入帧。
- `BattleSessionContext` 承载战斗会话数据与玩家操作：Pawn 数据投影、聚焦提交与循环、施法通道、阵营判定、战斗计时、副本键投影。
- `UnitShowManager` 只组装单位视图并驱动其生命周期、装配技能展示资源，不对外提供数据查询；UI 与相机统一经 `BattleSessionContext` 读取数据。
- 战斗输入：`BattleInputController` 采集移动方向与 3D 拾取，聚焦目标与 Tab 循环切换敌方目标。
- UI：主菜单、大厅、房间准备、单位选择、服务器管理面板，以及战斗 HUD 与战斗交互，含状态条、技能列表、Buff、伤害信息、计时与技能信息。
- 服务器进程管理：`ServerProcessHost` 以子进程拉起/停止服务器，查询式状态接口供 UI 轮询。

## 不负责

- 不实现网络传输与连接状态机，全部委托 `Client.Lobby` 与 `Client.Battle` 两个连接客户端。
- 不实现战斗结算、AI、仇恨与 Buff 规则，权威在服务端。
- 不承载服务端业务，服务器是独立子进程。
- UI 不直接持有网络对象，只消费 `IClientBattleService` 接口与 C# 事件。


## 依赖项

- Client 及其 Lobby/Battle 两端；共享层 Protocol、GameConfig、Entities、Battle.Domain 与 Battle.Logic。
