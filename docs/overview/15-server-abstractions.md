# DungeonChessBattle.Server.Abstractions

服务端抽象契约，所属分组 Server。定义服务端各领域模块与装配层之间协作的纯接口，只暴露原语类型，不依赖任何领域实现。职责边界见 `functional_boundary/15`。

## 四类契约

- **`IBattleRoomManager`** 房间生命周期协调原语：`StartRoomBattle`（创建并等待首帧初始化，返回端口）、`TryGetRoomPort`、`RegisterPlayer`（重连登记，仅恢复既有同名会话）、`ProcessPendingRoomCleanups`、`StopAll`、`ListRooms`。实现：Battle.Server `BattleRoomManager`。
- **`ILobbyBroadcaster`** 广播端口：`AddToRoomAsync` / `RemoveFromRoomAsync` / `SendToRoomAsync`。实现：Lobby.Server `SignalRBroadcaster`（SignalR Group）。
- **`IPlayerIdentityResolver`** 会话凭证 → 玩家记录主键解析：`ResolveRecordId`。实现：Server.DataStore `PlayerIdentityResolver`。消费方是回放服务端，它由此不必认识登录会话与凭证的签发方式。
- **`IReplayStore`** 回放归档存储端口：`Add`（战斗房间销毁时归档编码字节流与摘要）、`GetReplaysByPlayerId`（按玩家记录主键检索，最近在前）、`TryGetReplay`（按房间 ID 取字节流）。实现：Server.DataStore `InMemoryReplayStore`，写入方 Battle.Server、读取方 Replay.Server。

## 契约边界

- 入参与返回值限原生类型、字符串与纯原语 DTO。
- 零依赖契约库，供 Lobby.Server、Battle.Server、Replay.Server、Server.DataStore 与 Server.Host 共享，实现与调用方互不感知。
- 回放记录格式契约在 Replay.Shared，HTTP 契约在 Replay.Protocol；归档存储端口与摘要模型在本层。
- 本端口只映射不签发：凭证由大厅登录换发、随连接作废；解析不出主键就等于"这串凭证不代表任何人"，调用方据此拒绝。

