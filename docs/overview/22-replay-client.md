# DungeonChessBattle.Replay.Client

回放获取侧：服务端归档摘要列表与单场归档字节流下载。职责边界见 `functional_boundary/22`。

## 获取时序

```
GetServerListAsync(ct)  → GET /replay/list    服务端摘要列表
DownloadArchiveAsync(roomId, progress, ct)  → GET /replay/{roomId}  归档字节流，边收边回报进度
```

本层只做"从服务端拿事实"：**不解码、不门控、不缓存、不并集**。本地缓存、内容版本门控、列表并集与"可否重放"判定都是 Game 层回放浏览服务的消费决策。

## 边界

- 只认 `ReplayHttpRoutes` 契约（路由、`X-Dcb-Session` 头、URL 组装）；服务器根地址与会话凭证每次现取。
- 零 Godot 依赖、不绑定场景节点；全 async，抛任意线程都安全。
- 服务端侧不可用（未连接、未登录、401、断网、404）以状态返回或降级为空列表，不上抛异常；只有用户取消抛 `OperationCanceledException`，`HttpClient` 超时归网络错误。

## 注入与装配

- 注入两样：`Func<Uri>` 服务器根地址、`Func<string?>` 会话凭证，每次请求现取——凭证随重登换发，缓存下来就会用到作废的值。
- Godot 装配点在 `ServiceLocator.ReplayService`，它组合本客户端与 `ReplayCache` 裁决出每行视图结论供主线程消费；启动回放由面板对播放按钮显式触发，本层与浏览服务都不自动进入。本层只定义传输结果 `ReplayTransportStatus` / `ReplayDownloadResult`，解码与内容门控归 Game 层浏览服务。
