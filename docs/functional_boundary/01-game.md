# DungeonChessBattle.Game（Godot 主工程）

客户端工程，Godot 4.7 C#。场景、UI、战斗表现与网络驱动的最终装配层，不实现任何战斗规则与服务端业务。

## 职责

- 装配客户端服务与服务器子进程，驱动网络与输入。
- 路由战斗进出，互斥加载与释放战斗/回放两套组装场景，统一编排屏幕状态。
- 以统一数据源 `BattleSessionContext` 承载战斗会话读数，按装配切换在线与回放两路取数，向表现层与相机供数。
- 组装单位视图与技能展示资源，不对外提供数据查询。单位外观经展示索引定制（`IUnitView.ModelScene`/`BodyColor`），未声明即回落共享模板 `unit_game_show.tscn`。
- 经 `ResourceTables` 唯一入口读取技能/Buff/副本展示资源，场景模板依附资源文件；工程内的 `res://` 路径只有 `ResourceTables` 与 `BuiltinDisplayAssets` 两处持有。
- mod 装配编排：`ModManager` 按「扫描启用集 → 数据装配 → 内置展示先入注册表 → mod 声明后入注册表 → mod 条目落地成资源 → 发布索引」串起 `Game.Mod` 与 `GameConfig`。被 mod 声明过的条目以内置资源为模板复制后改写，未声明的字段保留模板值；内容里有但展示里没有的条目补占位资源，不让自检崩。
- 展示资源类与 `res://` 路径必须留在本工程：可被 `.tres`/`.tscn` 引用的脚本类以 `res://` 路径与 `script_class` 绑定，移出 Godot 工程目录即断引用。`Game.Shared` 只放契约，不放这些类；引擎预置场景的资源名登记在 `BuiltinDisplayAssets`。
- 采集战斗输入与目标拾取。
- 主菜单、大厅、房间准备、单位选择、mod 管理与战斗 HUD 等全部界面。mod 管理面板只呈现 `ModCatalog` 的扫描结果并转达启停，判定与落盘不在面板。
- 以子进程拉起与停止服务器，状态供 UI 查询。

## 边界外

- 不实现网络传输与连接状态机，全部委托大厅与战斗两个连接客户端。
- 不实现战斗结算、AI、仇恨与 Buff 规则，权威在服务端。
- 不承载服务端业务，服务器是独立子进程。
- UI 与会话投影层不持有网络对象：房间链路只消费 `IClientBattleSession` 契约与 C# 事件，诊断只消费快照 DTO；连接权力在门面，本层无绕过门面的连接入口。

## 依赖

- Client 及其 Lobby/Battle 两端；共享层 Lobby.Protocol、GameConfig、Battle.Entities、Battle.Shared、Battle.Logic 与 Game.Mod、Game.Shared。mod 数据面 `Battle.Mod` 经 `Game.Mod` 与 `GameConfig` 间接进入，本工程不直连。
