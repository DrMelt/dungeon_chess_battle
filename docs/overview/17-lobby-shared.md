# DungeonChessBattle.Lobby.Shared

大厅共享值类型库，存放跨服务端存储、协议传输与客户端展示共用的房间领域值类型。职责边界见 `functional_boundary/17`。

## 类型

- `RoomStatus`：房间状态枚举，Waiting / InProgress / Finished。存储层 `GameRoom.Status` 与协议 DTO `RoomListing.Status`、`RoomSnapshot.Status` 共用同一类型，避免跨层枚举映射。
