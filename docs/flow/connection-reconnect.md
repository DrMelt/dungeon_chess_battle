# 连接、进房与重连

一条端到端链：启动子进程 → 登录大厅 → 建房/加入 → 准备 → 开始战斗并重定向到房间端口 → 断开 → 重连恢复 → 收敛复位。跨 `Game`、`Client`、`Lobby.Client`、`Lobby.Server`、`Server.DataStore`、`Battle.Server`。

各模块内部机制不在本文：门面的主线程模型见 `overview/client`，登录会话与身份反查见 `overview/lobby`，凭证换发与房间状态的存储口径见 `overview/datastore`，房间侧会话与载体（服务端与在线端）见 `overview/battle`，子进程状态查询见 `overview/godot`，宿主装配与看护见 `overview/server`。

## 握手

- 客户端 `ServerProcessHost` 以 `--port` 传端口、`DCB_SERVER_PASSWORD` 传密码、`DCB_SERVER_PARENT_PID` 传父 PID 拉起子进程；就绪判定只有 TCP 端口探测一条依据，进程活着不等于能连。
- 服务端 `Program` 读回同两个环境变量装配；父进程消失或 PID 被复用（启动时间不符）时自行优雅退出。两端之间没有心跳协议，进程级存活靠看护，会话级存活靠客户端超时兜底。
- 大厅与房间共用同一宿主：大厅走 SignalR `/lobby`，房间是独立端口的 LiteNetLib，回放是同宿主同大厅端口的 HTTP。

## 状态机

`ClientConnectionState`：Idle → ConnectingLobby → InLobby → ConnectingRoom → InRoom → Reconnecting。

- `SetState` 是唯一转换入口，同时记录状态起始时间戳，超时判定只看它。
- 超时兜底 `HandleConnectTimeout`：对 ConnectingLobby / ConnectingRoom / Reconnecting 三个进行中状态计时 10 秒，超时断开活动客户端并复位。「进行中」状态若无上界，一次丢包的握手就把会话永久悬住。
- `IsConnected` 恒等于 InLobby 或 InRoom，不给中间态。

## 三条进房路径

进房端口统一走 `ReconnectToRoom`，三条路径共用同一次连接：

| 触发 | 来源事件 | 连接成功后 |
|---|---|---|
| 加入房间 | `OnRoomJoined` | `OnRoomJoined` → 准备界面 |
| 开始战斗重定向 | `OnPrepareBattleRedirect` | `OnBattleStarted` → 进入战斗 |
| 断线重连 | `RequestReconnectRoom` 通过校验 | `OnBattleStarted` → 恢复战斗 |

- 用 `_pendingJoinRoomId` / `_pendingBattleRoomId` 区分触发语义：前者交 `OnRoomJoined`，后者交 `OnBattleStarted`。同一处连接成功代码，语义由 pending 键决定，不由状态推断。
- 房间连接密钥是持久 `PlayerId`：`OnConnectionRequest` 端拿它查房间成员白名单，同一 `PlayerId` 已有活跃连接时关旧迎新。服务端不认「谁看起来像房主」。
- 重连登记的最后一道在 `RegisterPlayer`：只恢复房间既有同名会话，杜绝冒用他人 `PlayerId` 绑上别人的单位。身份依据是登录会话，不是连接。

## 断线自动重连

房间意外断开 → `AttemptReconnectToRoom`，全程处于 Reconnecting：

1. 重连大厅 `LobbyClient.Reconnect`（先清缓存再重建，旧连接代际作废，见 `overview/lobby` 的大厅客户端一节）。
2. 重新 `Login`，`_reconnectPendingLogin` 等待登录结果——凭证与登录会话同生命周期，断连即作废，没有新凭证就过不了服务端校验。
3. `RequestReconnectRoom` 经登录会话反查身份 + 房间密码校验，拿回房间端口。
4. 重定向回房间端口，按 `PlayerId` 恢复会话，重连方按 `BaselineSync` 全量重建视图。

断线期间的领域推进不丢（服务端载体不销毁、投影照常下行，见 `overview/battle`），但事件日志不补发——重连后 UI 的事件历史从断点继续，缺口不回填。

## 收敛

`ResetToNonRoomState` 是房间会话唯一收敛点：清缓存（roomId / 端口 / 密码）、复位状态机（有大厅连接回 InLobby，否则 Idle）、若原处于房间会话则触发 `OnBattleSessionLost`。重连失败、无缓存断开、连接超时与完全断开四类都汇聚到这一处，`MainScene` 据 `OnBattleSessionLost` 退出战斗，没有第二条退出路径。

主动离开 `LeaveRoom` 显式清缓存并复位到 InLobby：不清的话，离开后任何一次意外断开都会被误判成「对已离开房间的重连」，把用户拽回一个不属于他的房间。
