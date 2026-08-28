# DungeonChessBattle.Lobby.Client

大厅客户端，SignalR 传输，所属分组 Client。只承载大厅与准备阶段的请求与广播回调，不包含 LES 实体系统。职责边界见 `functional_boundary/03`。

## 连接管理

- 构建 `HubConnection` 连接 `http://{host}:{port}/lobby`，注册服务端广播回调：`On<RoomSnapshot>`（快照）、`On<RoomRedirect>`（准备→战斗重定向）。
- 连接代际 `_connectionVersion`：每次 `Connect` 递增。`StartAsync` 异步完成后检查代际是否过期，隔离旧连接的迟到回调干扰新连接。
- `Disconnect` 显式释放旧连接并触发 `OnFullyDisconnected`；`Reconnect` 先清缓存再重建。

## 请求与事件回传

- 请求模式：`RunHubCall` 检查连接状态后 fire-and-forget `InvokeAsync`，成功/失败结果经事件回调返回，请求异常统一记录日志。
- 请求事件映射：`CreateRoom` → `OnRoomCreated`、`JoinRoom` → `OnRoomJoined`、`ReconnectRoom` 成功 → `OnRedirectToRoom`（失败 → `OnReconnectFailed`）、`Login` → `OnLoginResult`。
- 回调全部发生在 SignalR 后台线程，消费方负责转主线程；门面 `GameClientService` 经主线程动作队列消费。

## 会话凭证

- 登录成功后服务端签发一个随机串（`LoginResult.SessionToken`），`LobbyClient` 存为 `SessionToken`，`Connect`/`Reconnect`/`Disconnect` 一并作废。
- 它的意义是让身份走出 SignalR：任何服务端 HTTP 端点都能凭它自证，不必再借大厅连接。当前唯一消费方是回放（`Replay.Client` 经 `GameClientService.SessionToken` 取用），大厅与房间业务一概不看它。
- 凭证与登录会话同生命周期：连接断开即撤销，重连后必须重新登录才会换发新凭证；同一连接再次登录会撤销旧凭证，不留双活身份。

## 快照缓存

- `_roomSnapshots`（ConcurrentDictionary）缓存每个房间最近一次完整快照，进房初始化经 `TryGetRoomSnapshot` 读取；断开/重连时清空。

