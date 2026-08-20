# DungeonChessBattle.Server.Abstractions

服务端抽象契约，所属分组 Server。定义服务端各领域模块与装配层之间协作的纯接口，只暴露原语类型，不依赖任何领域实现。

## 职责范围

- `IBattleRoomManager`：战斗房间服务器生命周期契约，开始战斗、端口查询、玩家重连登记、空房清理、停止与列表。
- `ILobbyBroadcaster`：大厅广播端口，向房间内连接分组推送消息，经 SignalR 等传输实现。

## 不负责

- 不包含领域类型与 DTO，入参与返回值限原生类型、字符串与既有契约。
- 不包含实现：广播由 Server.Lobby 的 `SignalRBroadcaster` 实现，房间生命周期由 Server.Battle 的 `BattleRoomManager` 实现。

## 依赖项

- 无。零依赖契约库，供 Server.Lobby、Server.Battle 与 Server.Host 三方共享。
