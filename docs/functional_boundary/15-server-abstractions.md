# DungeonChessBattle.Server.Abstractions

服务端抽象契约，所属分组 Server。定义服务端各领域模块与装配层之间协作的纯接口，只暴露原语类型，不依赖任何领域实现。

## 职责范围

- `IBattleRoomManager`：战斗房间服务器生命周期契约，开始战斗、端口查询、玩家重连登记（仅恢复既有同名会话）、空房清理、停止与列表。
- `ILobbyBroadcaster`：大厅广播端口，向房间内连接分组推送消息，经 SignalR 等传输实现。
- `IReplayStore`：回放存储契约，归档编码后的回放字节流与摘要，按玩家记录主键查询回放、按房间下载；含纯原语摘要 DTO `ReplaySummary`/`ReplayPlayer`（`PlayerRecordId` 为玩家记录主键，与战斗内玩家 ID 无关）。
- `IReplayDownloadTicketStore`：回放下载一次性凭证契约，Hub 参与者校验后签发、HTTP 下载端点验证消费。

## 不负责

- 不包含领域类型，入参与返回值限原生类型、字符串与纯原语 DTO；摘要 DTO 无领域依赖。
- 不包含实现：广播由 Server.Lobby 的 `SignalRBroadcaster` 实现，房间生命周期与回放存储 `InMemoryReplayStore` 由 Server.Battle 实现，回放凭证存储 `ReplayDownloadTicketStore` 由 Server.Lobby 实现。

## 依赖项

- 无。零依赖契约库，供 Server.Lobby、Server.Battle 与 Server.Host 三方共享。
