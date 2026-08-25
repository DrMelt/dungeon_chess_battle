# DungeonChessBattle.Client.Shared

客户端最小连接契约库，所属分组 Shared。职责边界见 `functional_boundary/20`。

## 契约机制

- `IClientConnection`：`IsConnected` / `Disconnect` / `Update` 三个成员。供门面 `GameClientService` 统一驱动大厅与房间两端；连接建立与完全断开事件由两端各自实现。实现方为大厅 `LobbyClient`（SignalR）与房间 `NetworkClientBase`（LiteNetLib + LES）。
