# DungeonChessBattle.Replay.Server

回放服务侧。承担回放列表与字节流的 HTTP 端点、会话凭证鉴权与参与者校验；不含身份签发与录制。

## 职责

- 端点映射：`MapReplayEndpoints` 注册列表与下载两条路由，宿主只负责调用。
- 凭证鉴权：从请求头取会话凭证，经解析端口换成玩家记录主键；换不到 401。
- 查询与取档：按主键取归档字节流、读其元数据块投影为协议 DTO；按主键校验参与者后交出归档字节。非参与者与不存在的回放同回 404，不暴露存在性。摘要不在存储里，读的是归档自身那份元数据。

## 边界外

- 不承载 SignalR，也不签发凭证：凭证由大厅登录流程签发与撤销，本库只解析它，不认识连接与登录动作。
- 不拥有身份体系：玩家记录主键由 `IPlayerIdentityResolver` 端口给出。
- 不实现录制与归档写入：录制与归档在 Battle.Server；存储实现在 Server.DataStore。
- 不解释回放内容：只读归档的元数据块取摘要，不解码输入轨道、不重放；重放在 Replay 引擎与客户端。

## 依赖

- Replay.Protocol（DTO、路由与序列化约定）、Replay.Shared（归档元数据块读取）、Server.Abstractions（身份解析与归档存储端口）。
- ASP.NET Core 共享框架 Microsoft.AspNetCore.App，仅用于端点映射。
