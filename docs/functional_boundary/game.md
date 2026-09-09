# DungeonChessBattle.Game（Godot 主工程）

客户端工程，Godot 4.7 C#。场景、UI、战斗表现与网络驱动的最终装配层，不实现任何战斗规则与服务端业务。

## 职责

- 装配客户端服务与服务器子进程，驱动网络与输入。
- 路由战斗进出，互斥加载与释放战斗/回放两套组装场景，统一编排屏幕状态。
- 以统一数据源 `BattleSessionContext` 承载战斗会话读数，按装配切换在线与回放两路取数，向表现层与相机供数。
- 组装单位视图与技能展示资源，不对外提供数据查询。单位外观经展示索引定制（`UnitDisplay.ModelScene`/`BodyColor`），未声明即回落共享模板 `unit_game_show.tscn`。
- 经 `ResourceTables` 唯一入口读取技能/Buff/副本展示资源；表为运行时空表，条目由展示装配以 `Mod*Resource` 填充，主工程不再持有任何技能/Buff/副本 `.tres`。工程内 `res://` 路径只由 `BuiltinDisplayAssets` 的引擎预置场景持有（`Game.Shared` 的 `DisplayAssetIds` 登记资源名）。
- mod 装配编排：扫描后交 `GameConfig` 做数据装配，把「引擎侧展示注册」「展示资源包挂载」与「mod 条目落地成资源」三个委托交进 `ModAssets.Assemble`，展示装配内部次序由 `Game.Mod.Manager` 保证。展示数据全部来自 mod 展示代码；内容里有的条目没展示时补占位资源。
- 可被 `.tres`/`.tscn` 引用的脚本类与 `res://` 路径必须留在本工程：`Game.Mod.*` 三库与 `Game.Shared` 都不放这些类；引擎预置场景的映射登记在 `BuiltinDisplayAssets`，资源名常量在 `Game.Shared` 的 `DisplayAssetIds`。
- 采集战斗输入与目标拾取。
- 主菜单、大厅、房间准备、单位选择、mod 管理与战斗 HUD 等全部界面。mod 管理面板只呈现 `ModCatalog` 的扫描结果并转达启停，判定与落盘不在面板。
- 以子进程拉起与停止服务器，状态供 UI 查询。

## 边界外

- 不实现网络传输与连接状态机，全部委托大厅与战斗两个连接客户端。
- 不实现战斗结算、AI、仇恨与 Buff 规则，权威在服务端。
- 不承载服务端业务，服务器是独立子进程。
- UI 与会话投影层不持有网络对象：房间链路只消费 `IClientBattleSession` 契约与 C# 事件，诊断只消费快照 DTO；连接权力在门面，本层无绕过门面的连接入口。

## 依赖

- Client 及其 Lobby/Battle 两端；共享层 Lobby.Protocol、GameConfig、Battle.Entities、Battle.Shared、Battle.Logic、Game.Shared 与 Game.Mod.Manager、Game.Mod.Shared。mod 开发锚点 `Game.Mod.Interface` 只面向 mod，本工程不引；数据面 `Battle.Mod.Manager`、`Battle.Mod.Interface` 与 `Battle.Mod.Shared` 经 `Game.Mod.Manager` 与 `GameConfig` 间接进入，本工程不直连。
