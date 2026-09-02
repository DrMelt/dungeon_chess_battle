# 客户端装配与契约域内部机制

覆盖 `DungeonChessBattle.Client` 门面；`Client.Shared` 只有边界描述，无域内机制。两端客户端的传输机制随各自业务域记录：大厅客户端见 `overview/lobby`，房间客户端与在线端回填见 `overview/battle`。连接状态机的跨域时序见 `flow/connection-reconnect`，下行同步链见 `flow/battle-state-sync`；模块边界见 `functional_boundary/02`、`20`。

## 门面

- 持有 `LobbyClient`（SignalR）与 `RoomBattleClient`（LiteNetLib + LES）两个持久实例，经 `IClientConnectionFactory` 创建——该接口是传输类型的唯一实例化点。实例只存在于门面内部，对外一律给抽象：房间链路给 `IClientBattleSession` 契约与 `RoomNetworkStatus` 快照，大厅链路给 `Request*` 方法。门面公开签名不含传输类型，连接发起权力由门面独占。
- 主线程驱动：LiteNetLib `NetManager` 非线程安全，SignalR 后台线程回调一律入队 `_mainThreadActions`，由 Godot 主线程每帧 `Update` 消费后再驱动两端网络轮询。所有对房间客户端的操作收敛主线程。
- 状态机、超时兜底、三条进房路径、断线重连与 `ResetToNonRoomState` 收敛见 `flow/connection-reconnect`。
- 不含回放获取：`Replay.Client` 直连服务端 HTTP 端点，门面不转发回放请求，只经 `SessionToken` 透传会话凭证（凭证归属与生命周期见 `overview/lobby`）。
- 另存 `LobbyPort`：`Port` 会随进入房间重定向变成房间端口，而与大厅同宿主同端口的 HTTP 端点（回放）地址不随之变。
