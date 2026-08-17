# DungeonChessBattle.Server.Lobby

大厅服务器，所属分组 Server。承载大厅 SignalR 传输端点、业务实现与协调门面，经 `IRoomServerManager` 契约编排战斗房间生命周期。

## 职责范围

- SignalR Hub `LobbyHub` 端点与广播端口实现 `SignalRBroadcaster`。
- 大厅业务 `GameLobby`：创建/加入/离开房间、招募板列表、准备单位增删、准备状态设置。
- 协调门面 `GameServer`：分派大厅请求、连接断开清理、开始战斗与断线重连编排。
- 房间快照组装与广播。
- 服务器密码切片 `LobbyServerConfig`，由装配层从 `ServerConfig` 映射注入。

## 不负责

- 不感知状态存储实现，经 `IGameStateStore` 读写。
- 战斗房间服务器实现 `BattleRoomServer` 不在本项目，经 `IRoomServerManager` 契约调用。
- 不承载进程装配：Kestrel、DI 组合根与进程看护由 Server.Host 承担。

## 依赖项

- Protocol、Server.StateStore.Abstractions、Server.Abstractions（契约）、Battle.Domain、GameConfig（副本键解析）。
- ASP.NET Core 共享框架 Microsoft.AspNetCore.App，承载 Hub 与 IHubContext。
