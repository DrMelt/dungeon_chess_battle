# DungeonChessBattle.Server.StateStore

服务器状态存储实现，所属分组 Server。当前唯一实现为基于 `ConcurrentDictionary` 的进程内版本。

## 职责范围

- 实现 `IGameStateStore`：房间配置、密码、房主、准备状态、连接归属、playerId 映射与准备单位表。
- 房间级锁表串行化同房间读改写，对外提供只读快照。

## 不负责

- 不含业务逻辑，大厅与战斗业务由上层消费。
- 战斗单位状态不进入 Store，由 Logic 层权威持有。
- 锁条目不随房间删除回收，避免 ABA 竞态。


## 与周边协作

- 上游：Server.Host 装配层注入；Server.Lobby 与 Server.Battle 业务消费。
