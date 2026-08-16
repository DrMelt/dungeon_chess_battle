# DungeonChessBattle（Godot 主工程）

客户端工程，Godot 4.7 C#。场景、UI、战斗表现与网络驱动的最终装配层。不实现任何战斗规则与服务端业务。

## 职责范围

- 服务装配：`ServiceLocator` 装配 `GameClientService` 与 `ServerProcessHost`，`GameClientDriver` 主线程每帧驱动网络与输入。
- 战斗编排外壳：`MainScene` 进出战斗、订阅战斗启动与阶段事件、应用副本环境主题、仲裁屏幕状态机；`BattleUnitManager` 组装单位视图并桥接服务端事件。
- 战斗输入：`BattleInputController` 采集移动方向与 3D 拾取，聚焦目标与 Tab 循环切换敌方目标。
- UI：主菜单、大厅、房间准备、单位选择、服务器管理面板，以及战斗 HUD 与战斗交互，含状态条、技能列表、Buff、伤害信息、计时与技能信息。
- 服务器进程管理：`ServerProcessHost` 以子进程拉起/停止服务器，查询式状态接口供 UI 轮询。

## 不负责

- 不实现网络传输与连接状态机，全部委托 Client 两端连接客户端。
- 不实现战斗结算、AI、仇恨与 Buff 规则，权威在服务端。
- 不承载服务端业务，服务器是独立子进程。
- UI 不直接持有网络对象，只消费 `IClientBattleService` 接口与 C# 事件。


## 与周边协作

- 消费 `IClientBattleService` 契约与 GameClientService 门面事件。
- 引用 Protocol、GameConfig、Entities、Battle.Domain 与 Battle.Logic 共享层。
