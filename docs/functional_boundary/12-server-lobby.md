# DungeonChessBattle.Server.Lobby

大厅服务器，所属分组 Server。承载大厅 SignalR 传输端点、业务实现与协调门面，经 `IBattleRoomManager` 契约编排战斗房间生命周期。

## 职责范围

- SignalR Hub `LobbyHub` 端点与广播端口实现 `SignalRBroadcaster`。
- 大厅业务 `GameLobby`：登入会话登记、创建/加入/离开房间、招募板列表、准备单位增删、准备状态设置；房间业务玩家名一律从登录会话反查，不信任客户端提交。
- 协调门面 `GameServer`：分派大厅请求、连接断开清理（含登录会话）、开始战斗与断线重连编排；重连以登录会话为身份依据，仅恢复房间既有同名会话。
- 回放查询与下载：身份从登录会话反查，登录名经玩家记录注册表解析为稳定主键，查询经 `IReplayStore` 契约仅返回该记录的回放摘要；下载经参与者校验后签发一次性凭证，回放字节由 Server.Host 的 HTTP 端点凭凭证流式获取。已知局限：记录主键由登录名派生，同名玩家共享主键导致回放互见，注册表进程内只增不删。
- 房间快照组装与广播。
- 服务器密码切片 `LobbyServerConfig`。

## 不负责

- 不感知状态存储实现，经 `IGameStateStore` 读写。
- 战斗房间服务器实现 `BattleRoomServer` 不在本项目，经 `IBattleRoomManager` 契约调用。
- 不承载进程装配：Kestrel、DI 组合根与进程看护由 Server.Host 承担。

## 依赖项

- Protocol、Server.StateStore.Abstractions、Server.Abstractions（契约）、Battle.Domain、GameConfig（副本键解析）。
- ASP.NET Core 共享框架 Microsoft.AspNetCore.App，承载 Hub 与 IHubContext。
