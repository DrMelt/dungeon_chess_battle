# DungeonChessBattle.Server.Abstractions

服务端抽象契约，所属分组 Server。定义服务端各领域模块与装配层之间协作的纯接口，只暴露原语类型，不依赖任何领域实现。职责边界见 `functional_boundary/15`。

## 四类契约

- **`IBattleRoomManager`** 房间生命周期协调原语：`StartRoomBattle`（创建并等待首帧初始化，返回端口）、`TryGetRoomPort`、`RegisterPlayer`（重连登记，仅恢复既有同名会话）、`ProcessPendingRoomCleanups`、`StopAll`、`ListRooms`。实现：Server.Battle `BattleRoomManager`。
- **`ILobbyBroadcaster`** 广播端口：`AddToRoomAsync` / `RemoveFromRoomAsync` / `SendToRoomAsync`。实现：Server.Lobby `SignalRBroadcaster`（SignalR Group）。
- **`IReplayStore`** 回放存储：`Add`（以房间 ID 主键幂等归档）、`GetReplaysByPlayerId`（按玩家记录主键查询）、`TryGetReplay`。实现：Server.Battle `InMemoryReplayStore`。
- **`IReplayDownloadTicketStore`** 一次性下载凭证：`Issue` / `TryConsume`。实现：Server.Lobby `ReplayDownloadTicketStore`。

## 契约边界

- 入参与返回值限原生类型、字符串与纯原语 DTO（`ReplaySummary` / `ReplayPlayer` 无领域依赖，`PlayerRecordId` 为玩家记录主键，与战斗内玩家 ID 无关）。
- 零依赖契约库，供 Server.Lobby、Server.Battle 与 Server.Host 三方共享，实现与调用方互不感知。

