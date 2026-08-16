# DungeonChessBattle.Server.StateStore.Abstractions

服务器状态存储抽象，所属分组 Server。定义大厅级房间与玩家准备状态的存储契约与快照模型，不绑定具体存储实现。

## 职责范围

- 存储门面 `IGameStateStore`，组合 `IRoomStateStore` 与 `IPlayerStateStore` 两个子接口。
- 房间状态存储：注册/查询房间、招募板列表、密码校验、状态与人数维护、清理。
- 玩家状态存储：成员登记与归属、准备状态、房主转让、准备单位增删、玩家名解析。
- 快照与数据模型：`GameRoom`、`RoomStateSnapshot`、`PlayerReadyState`、`UnitSelection`。

## 不负责

- 不包含具体存储实现，持久化方案在装配层替换。
- 不纳入网络连接密钥与战斗房间会话，它们分属网络层与战斗房间私有。
- 战斗单位状态不在此模型，由 BattleEngine 面向 `IBattleUnit` 权威持有。


## 与周边协作

- 上游：Server.Host（DI 注入）、Server.Lobby（大厅业务）、Server.Battle（取准备单位、重连资格校验）。
- 下游实现：`Server.StateStore` 的 `InMemoryGameStateStore`。
