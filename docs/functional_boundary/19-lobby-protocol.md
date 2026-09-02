# DungeonChessBattle.Lobby.Protocol

大厅网络契约唯一权威来源。服务端与客户端两侧共用，消除大厅协议字符串与 DTO 魔法值。

## 职责

- 网络契约常量：大厅 SignalR Hub 方法名 `HubMethods`。
- 网络 DTO：大厅请求与结果、招募板条目、房间快照。
- `LoginResult.SessionToken`：登录成功后服务端签发的连接级会话凭证，供服务端 HTTP 端点自证身份；属大厅身份体系，不属任何业务分组。

## 边界外

- 不含业务实现与运行时逻辑，纯 .NET 类库，无第三方运行时依赖。
- 不含网络默认值与字段长度约束，网络默认值归 Battle.Entities，字段长度约束归 Battle.Shared。
- 回放记录格式与编解码，归 Replay.Shared；回放网络契约，归 Replay.Protocol。
- 不受理连接建立与重连，连接事件由大厅与房间两端各自实现。

## 依赖

- Lobby.Shared（房间状态枚举）。
