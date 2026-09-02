# DungeonChessBattle.Client.Shared

客户端最小连接契约库。定义客户端连接抽象，供门面统一驱动大厅 SignalR 与房间 LiteNetLib+LES 两端。

## 职责

- `IClientConnection`：`IsConnected` / `Disconnect` / `Update` 三个成员，供 `GameClientService` 统一驱动大厅与房间两端。

## 边界外

- 不含协议 DTO 与方法名，归 Lobby.Protocol。
- 不含连接建立与重连实现，连接事件由大厅与房间两端各自实现。
- 不含网络默认值，归 Battle.Entities；字段长度约束归 Battle.Shared。

## 依赖

- 无：纯 .NET 类库。
