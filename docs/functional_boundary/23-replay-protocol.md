# DungeonChessBattle.Replay.Protocol

回放 HTTP 契约唯一权威来源，所属分组 Shared。服务端端点与客户端调用共用，消除回放的路径、请求头、DTO 与序列化约定的魔法值。

## 职责

- 网络 DTO：回放摘要列表、摘要条目与参与玩家条目。
- 路由与请求头 `ReplayHttpRoutes`：路径前缀、两条路由、会话凭证头名与 URL 组装，服务端映射与客户端调用同源。
- 序列化约定 `ReplayJson`：两端共用一份 `JsonSerializerOptions`，序列化不经 SignalR 之后，各配一份就会让字段静默为 null。

## 边界外

- 不含业务实现与运行时逻辑，纯 .NET 类库，无第三方运行时依赖。
- 不含回放记录格式与编解码、不定义回放归档存储契约，都归 Replay.Shared。
- 不定义跨库协作接口：回放两端之间只有 HTTP 报文，大厅连接不参与。

## 依赖

- 无。
