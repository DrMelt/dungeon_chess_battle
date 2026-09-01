# DungeonChessBattle.Replay.Client

回放获取侧，所属分组 Client。只向服务端取回放事实：服务端归档摘要列表与单场归档字节流下载。零 Godot 依赖，不含缓存、解码、门控、并集与重放。

## 职责

- 服务端列表：`GetServerListAsync`，服务端侧不可用降级为空列表，不抛。
- 字节流下载：`DownloadArchiveAsync`，`HttpClient` 流式读取，回报进度、支持取消与超时；401、404 与网络失败分类返回。
- 结果形状只含传输语义：`ReplayTransportStatus`（Success / Unauthorized / NotFound / NetworkError）与 `ReplayDownloadResult`；解码、内容版本门控与"可否重放"判定不在此层。

## 边界外

- 不做本地缓存、条目枚举、解码、内容版本门控与列表并集：归 Game 层 `ReplayService` + `ReplayCache`。
- 不构建战斗世界、不推进帧、不含播放控制，重放在 Replay 引擎。
- 不含 UI、节点与屏幕态切换，呈现在 Godot 主工程。
- 不建立连接、不登录、不签发凭证：服务器根地址与会话凭证都由注入的委托现取。
- 不定义回放归档存储契约，归 Replay.Shared，实现在 Server.DataStore。

## 依赖

- Replay.Protocol（DTO、路由与序列化约定）。
