# DungeonChessBattle.Server.StateStore

服务器状态存储实现，所属分组 Server。当前唯一实现为基于并发字典的进程内版本。

## 职责范围

- 实现存储契约：房间配置、密码、房主、准备状态、连接归属、玩家标识映射与准备单位表。
- 房间级锁表串行化同房间读改写，对外提供只读快照。

## 不负责

- 不含业务逻辑，大厅与战斗业务由上层消费。
- 战斗单位状态不进入存储，由逻辑层权威持有。


## 依赖项

- Server.StateStore.Abstractions（契约）、Protocol（DTO 模型）。
