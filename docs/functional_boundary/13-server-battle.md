# DungeonChessBattle.Server.Battle

战斗房间服务层，所属分组 Server。每个房间拥有独立的 LES 实体服务器与战斗世界，运行于独立线程。

## 职责范围

- 单房间 LES 服务器：独立 `NetManager`、`ServerEntityManager` 与 `BattleScene`，独立后台线程固定步长驱动。
- 房间生命周期：创建/查找/销毁、端口池分配回收、空房清理队列。
- `RoomServerManager` 实现 `IRoomServerManager` 契约，向外只暴露端口等原语，供大厅协调层编排。
- 初始化与登场：迁移准备期单位、按副本配置生成敌人、构建移动物理场景。
- 战斗循环编排：AI 前置推进与战斗推进收编进 LES 逻辑 tick（`BattleLoop`），转发 `IBattleScene.ApplyDecisions` 与 `Tick`，移动与施法经 `BattleScene` 与玩家共用同一权威入口。
- 玩家会话与连接密钥校验、断线重连；连接状态是会话本地数据（`PlayerSession`），不产生网络同步实体。

## 不负责

- 不实现大厅业务与传输层广播，广播契约 `ILobbyBroadcaster` 在 Server.Abstractions。
- 不为客户端结算：结算权威都在 BattleScene，客户端只有移动预测。
- 大厅线程不触碰 EntityManager，只做生命周期控制。

## 依赖项

- 共享层 Battle.Logic、Battle.Domain、Entities、GameConfig 与 Protocol；存储层经 `IGameStateStore`；契约层 Server.Abstractions。
