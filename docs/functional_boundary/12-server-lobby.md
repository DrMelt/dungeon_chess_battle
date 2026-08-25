# DungeonChessBattle.Lobby.Server

大厅服务器，所属分组 Server。承载大厅 SignalR 传输端点、业务实现与协调门面，经房间管理契约编排战斗房间生命周期。

## 职责

- SignalR Hub 端点与广播端口实现。
- 大厅业务：登录会话、创建/加入/离开房间、招募板列表与准备状态；身份一律经登录会话反查。
- 协调门面：分派大厅请求、连接断开清理、开始战斗与断线重连编排。
- 房间快照组装与广播。
- 回放查询与下载：身份反查、参与者校验与一次性下载凭证签发。

## 边界外

- 不感知数据存储实现，经数据存储契约读写。
- 战斗房间服务器实现不在本项目，经房间管理契约调用。
- 不承载进程装配：Kestrel、DI 组合根与进程看护由服务器宿主承担。

## 依赖

- Protocol、Server.DataStore.Shared、Server.Abstractions（契约）、Battle.Shared、GameConfig（副本键解析）。
- ASP.NET Core 共享框架 Microsoft.AspNetCore.App，承载 Hub 与广播上下文。
