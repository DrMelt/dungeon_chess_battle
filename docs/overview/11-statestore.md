# DungeonChessBattle.Server.StateStore

服务器状态存储实现，所属分组 Server。当前唯一实现为基于 `ConcurrentDictionary` 的进程内版本。职责边界见 `functional_boundary/11`。

## 数据表

- 房间配置、房间密码、房主名、准备状态（房间 → 玩家名 → 是否就绪）、连接归属（connectionId → 房间+玩家名）、登录会话（connectionId → 登录名）、玩家记录注册表（登录名 → 主键）、playerId 映射（房间 → 玩家名 → playerId）、准备单位列表。

## 并发策略

- 房间级锁表：每房间一个常驻锁对象（`GetRoomLock`），串行化同房间读改写，保护 `List<T>` 操作与可变模型字段。锁对象不随房间删除回收，避免新旧锁错位竞态。
- 对外读接口返回深拷贝（`CloneRoom` / 快照），阻止调用方绕过房间锁改写 Store 内可变对象。
- 跨房间枚举（`ListActiveRooms`）不加锁，依赖 ConcurrentDictionary 弱一致性快照，字段可能略旧可接受。

## 关键语义

- 成员移除 `RemovePlayerByConnection`：准备阶段（Waiting）还执行人数扣减、单位清理、房主转让（最后一人退出删除房间全部状态）；战斗中（InProgress）仅基础清理，生命周期由 BattleRoomManager 负责。
- 准备状态与单位增删都校验"已准备不可改"；房主身份不参与准备判定。
- 玩家记录注册表进程内只增不删，登录名 → 主键派生；同名玩家共享主键导致回放互见，属已知局限。

