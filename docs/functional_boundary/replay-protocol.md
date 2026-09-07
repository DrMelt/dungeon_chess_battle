# DungeonChessBattle.Replay.Protocol

回放 HTTP 契约唯一权威来源。服务端端点与客户端调用共用，消除回放的路径、请求头、DTO 与序列化约定的魔法值。

## 职责

- 网络 DTO：回放摘要列表、摘要条目与参与玩家条目。
- `ReplaySummaryDto.From`：归档元数据 → 摘要条目的唯一投影，服务端列归档与客户端列本地副本共用。
- 路由与请求头 `ReplayHttpRoutes`：路径前缀、两条路由、会话凭证头名与 URL 组装，服务端映射与客户端调用同源。
- 序列化约定 `ReplayJson`：两端共用一份 `JsonSerializerOptions`，序列化不经 SignalR 之后，各配一份就会让字段静默为 null。

## 边界外

- 不含业务实现与运行时逻辑：只有一条纯字段投影，无 I/O、无状态；本库自身不触序列化，经 Replay.Shared 间接带上 MessagePack。
- 不含回放记录格式与容器编解码，归 Replay.Shared——引用它只为取 `ReplayMeta` 这个源形状；不含回放归档存储契约，归 Server.Abstractions。
- 不定义跨库协作接口：回放两端之间只有 HTTP 报文，大厅连接不参与。

## 依赖

- Replay.Shared（`ReplayMeta` 源形状）。
