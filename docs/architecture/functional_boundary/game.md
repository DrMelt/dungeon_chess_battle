# DungeonChessBattle.Game

客户端主工程。场景、UI、战斗表现与网络驱动的最终装配层，不实现任何战斗规则与服务端业务。

## 职责

- 装配客户端服务与服务器子进程，驱动网络与输入。
- 路由战斗进出，互斥加载与释放战斗/回放两套组装场景，统一编排屏幕状态。
- 以统一数据源承载战斗会话读数，按装配切换在线与回放两路取数。
- 组装单位视图与技能展示资源，经展示索引定制单位外观，未声明回落共享模板。
- 经唯一展示取数入口读取技能、Buff、单位、副本展示数据，其中技能、Buff、副本三张表由展示装配填充；技能范围提示与副本环境场景随 mod 包提供。
- 经数据面装配产物取单位目录、内容注册表与内容修订号，供单位选择、副本环境实例化与内容一致性门控。
- mod 装配编排：扫描后交 Battle.Mod.Manager 做数据面装配，把资源包挂载与条目落地两步交进展示装配；挂载步骤兼把展示程序集里的脚本类注册进引擎脚本系统；技能、Buff、副本三张资源表缺展示数据的条目补占位资源，装配产物交宿主持有。
- 引擎自身场景与 UI 的脚本类与资源路径留在本工程；表现效果资源随 mod 包提供。
- 采集战斗输入与目标拾取。
- 全部界面：主菜单、大厅、房间准备、单位选择、mod 管理与战斗 HUD。mod 管理面板只呈现扫描结果与启停转达。
- 以子进程拉起与停止服务器，状态供 UI 查询。

## 边界外

- 不实现网络传输与连接状态机，全部委托大厅与战斗两个连接客户端。
- 不实现战斗结算、AI、仇恨与 Buff 规则，权威在服务端。
- 不承载服务端业务，服务器是独立子进程。
- UI 层不持有网络对象：房间链路只消费会话接口与事件，连接权力在门面。

## 依赖

- Client、Battle.Client、Lobby.Shared、Lobby.Protocol、Battle.Shared、Battle.Config.Shared、Battle.Runtime.Shared、Battle.Entities、Battle.Logic、Battle.Config.Registry、Replay、Replay.Shared、Replay.Protocol、Replay.Client、Game.Shared、Game.Mod.Shared、Game.Mod.Manager。
- 不直连：Lobby.Client 经 Client 门面组装；Battle.Mod.Manager、Battle.Mod.Interface 与 Battle.Mod.Shared 经 Game.Mod.Manager 间接进入；Game.Mod.Interface 只面向 mod。
