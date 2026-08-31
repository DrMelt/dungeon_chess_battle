# DungeonChessBattle.Client

网络客户端门面与连接状态机，所属分组 Client。向下组装大厅与房间两个连接客户端，向上对 Godot 层暴露统一连接模型。职责边界见 `functional_boundary/02`。

## 双客户端与主线程模型

- 持有 `LobbyClient`（SignalR）与 `RoomBattleClient`（LiteNetLib + LES）两个持久实例，经 `IClientConnectionFactory` 创建，该接口是传输类型的唯一实例化点。实例只存在于门面内部，对外一律给抽象：房间链路给 `IClientBattleSession` 契约与 `RoomNetworkStatus` 快照，大厅链路给 `Request*` 方法，门面公开签名不含传输类型。
- 主线程驱动：LiteNetLib NetManager 非线程安全，SignalR 后台线程回调一律入队 `_mainThreadActions`，由 Godot 主线程每帧 `Update` 消费后再驱动两端网络轮询。所有对房间客户端的操作收敛主线程。
- 不含回放获取：`Replay.Client` 直连服务端 HTTP 端点，门面不转发回放请求，只经 `SessionToken` 透传 `LobbyClient` 持有的会话凭证。
- 另存 `LobbyPort`：`Port` 会随进入房间重定向变成房间端口，而与大厅同宿主同端口的 HTTP 端点（回放）地址不随之变。

## 连接状态机

- 状态 `ClientConnectionState`：Idle → ConnectingLobby → InLobby → ConnectingRoom → InRoom → Reconnecting。`SetState` 是唯一转换入口，同时记录状态起始时间戳。
- 连接超时兜底：`HandleConnectTimeout` 对 ConnectingLobby/ConnectingRoom/Reconnecting 三个进行中状态计时 10 秒，超时断开活动客户端并复位，杜绝卡死。

## 重定向与重连

- 进入房间端口统一走 `ReconnectToRoom`：加入房间（OnRoomJoined）、准备→战斗启动（OnPrepareBattleRedirect）、断线重连（OnRedirectToRoom）三条路径共用。以持久 `PlayerId` 作连接密钥，服务端白名单校验。
- 用 `_pendingJoinRoomId` / `_pendingBattleRoomId` 区分连接成功后的触发语义：前者触发 `OnRoomJoined`，后者触发 `OnBattleStarted`。
- 断线自动重连：房间意外断开 → `AttemptReconnectToRoom`。全程处于 Reconnecting：先重连大厅 → 登入（`_reconnectPendingLogin` 等待登录结果）→ `RequestReconnectRoom` 经服务端校验 → 重定向回房间端口。失败/超时统一由 `ResetToNonRoomState` 兜底复位。

## 会话收敛

- `ResetToNonRoomState` 是房间会话唯一收敛点：清缓存（roomId/端口/密码）、复位状态机、原处于房间会话时触发 `OnBattleSessionLost`，战斗编排层据此退出战斗。重连失败、无缓存断开、连接超时与完全断开都汇聚到此。
- 主动离开 `LeaveRoom` 显式清缓存并复位到 InLobby，防止后续意外断开误触发对已离开房间的自动重连。

