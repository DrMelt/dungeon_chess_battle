# DungeonChessBattle.Client

网络客户端门面与连接状态机，所属分组 Client。向下组装大厅与房间两个连接客户端，向上对 Godot 层暴露统一连接模型。

## 职责范围

- 持有 `LobbyClient` 与 `RoomBattleClient` 两个持久实例，经 `IClientConnectionFactory` 创建，门面不依赖传输实现。
- 维护连接状态机 `ClientConnectionState`：Idle、ConnectingLobby、InLobby、ConnectingRoom、InRoom、Reconnecting。
- 断线重连：缓存 `playerId`、`roomId`、`roomPort` 与房间密码，经 SignalR `ReconnectRoom` 校验资格后重连房间端口。
- 把两端客户端事件转换为 C# 事件，向 Godot 层暴露请求入口。

## 不负责

- 不实现 SignalR 与 LiteNetLib 具体传输。
- 不含大厅与战斗业务逻辑，只做连接编排。
- 不在后台线程操作 LiteNetLib，回调只入队主线程动作队列。


## 与周边协作

- 消费方：Godot 主工程的 `ServiceLocator.ClientService` 与 `GameClientDriver`。
