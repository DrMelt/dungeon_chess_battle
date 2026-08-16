# DungeonChessBattle.Server.Lobby

大厅业务层，所属分组 Server。处理大厅 SignalR 请求的业务逻辑，不感知传输与状态存储实现。

## 职责范围

- 创建/加入/离开房间、招募板列表、准备单位增删、准备状态设置。
- 房间快照组装与广播，经 `ILobbyBroadcaster` 端口投递。
- 服务器密码切片 `LobbyServerConfig`，由装配层从 `ServerConfig` 映射注入。

## 不负责

- 不依赖具体传输：SignalR 投递由 `ILobbyBroadcaster` 反转实现，不感知战斗侧配置。
- 不感知状态存储实现，经 `IGameStateStore` 读写。
- 战斗房间生命周期不在此范围，由 `RoomServerManager` 承担。

## 与周边协作

- 上游：`Server.Host` 的 `GameServer` 协调器，经 `ILobbyBroadcaster` 与 `IGameStateStore` 注入。
