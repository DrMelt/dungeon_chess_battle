# DungeonChessBattle.Server.DataStore

服务器数据存储实现。当前唯一实现为基于并发字典的进程内版本。

## 职责

- 实现存储契约：房间配置、密码、房主、准备状态、连接归属、玩家标识映射与准备单位表。
- 实现回放归档存储契约：进程内归档、玩家记录索引与保留场数上限。
- 持有登录会话与会话凭证，会话身份解析就地完成。
- 兑现契约上的同房间读改写原子性，对外读提供快照。

## 边界外

- 不含业务逻辑，大厅与战斗业务由上层消费。
- 战斗单位状态不进入存储，由逻辑层权威持有。

## 依赖

- Server.DataStore.Shared（存储门面契约）、Server.Abstractions（`IReplayStore` 归档与 `IPlayerIdentityResolver` 身份解析端口）、Battle.Shared（字段长度常量）、GameConfig（默认副本键）、Lobby.Shared（房间状态枚举）。
