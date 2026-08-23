# DungeonChessBattle.Server.StateStore.Abstractions

服务器状态存储抽象，所属分组 Server。定义大厅级房间与玩家准备状态的存储契约与快照模型，不绑定具体存储实现。职责边界见 `functional_boundary/10`。

## 门面组合

- `IGameStateStore` 组合两个子接口：`IRoomStateStore`（房间级）+ `IPlayerStateStore`（玩家级）。业务层只面向门面，存储引擎可在装配层替换。
- 并发语义：任何线程都可安全调用；同一房间内的读改写由实现保证原子性。

## 房间状态契约

- 房间注册（`TryRegisterRoomWithHost` 原子注册房间 + 房主登记）、招募板列表、密码校验、状态与人数维护、成员校验（`IsRoomMember`）、清理。
- 连接密钥与战斗房间会话不在本模型：网络密钥属网络层，战斗房间会话属战斗房间私有。

## 玩家状态契约

- 连接归属：connectionId → 房间 ID 与玩家名，房主判定与反查。
- 登录会话：连接登记服务端权威玩家名，房间与回放业务统一从登录会话反查身份。
- 玩家记录注册表：登录名 → 稳定主键（首次自动登记），回放按主键归档与查询。
- 准备状态：非房主设置，未选单位不可准备；房主退出转让。
- 准备单位：增删校验（已准备不可改），阵营选项键由副本配置权威解析。

## 快照模型

- `GameRoom` 房间配置、`RoomStateSnapshot` 准备状态、`PlayerReadyState`、`UnitSelection` 单位选择记录。
- 战斗单位状态不在此模型，由战斗世界自持的领域单位 `BattleUnit` 权威持有。

