# DungeonChessBattle.Lobby.Server

大厅服务器。承载大厅长连接端点、业务实现与协调门面，经房间生命周期接口编排战斗房间。

## 职责

- 长连接端点与广播端口实现。
- 大厅业务：登录会话、创建、加入与离开房间、招募板列表与准备状态；身份一律经登录会话反查。
- 协调门面：分派大厅请求、连接断开清理、开始战斗与断线重连编排；房间启动失败即应答失败，不改房间状态。
- 房间快照组装与广播。

## 边界外

- 不感知数据存储实现，经数据存储接口读写。
- 战斗房间服务器实现不在本项目，经房间生命周期接口调用。
- 不含回放：查询、鉴权与端点归 Replay.Server；本库只在登录成功时签发会话凭证。
- 不承载进程装配：服务端宿主框架装配与进程看护由服务器宿主承担。

## 依赖

- Lobby.Shared、Lobby.Protocol、Server.DataStore.Shared、Battle.Shared、Battle.Config.Shared、Battle.Server.Shared、Session.Shared。
