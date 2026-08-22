# DungeonChessBattle.Server.Battle

战斗房间服务层，所属分组 Server。每个房间拥有独立的 LES 实体服务器与战斗世界，运行于独立线程。

## 职责范围

- 单房间 LES 服务器：独立 `NetManager`、`ServerEntityManager` 与 `BattleScene`，独立后台线程固定步长驱动。
- 房间生命周期：创建/查找/销毁、端口池分配回收、空房清理队列。
- `BattleRoomManager` 实现 `IBattleRoomManager` 契约，向外只暴露端口等原语，供大厅协调层编排。
- 初始化与登场：迁移准备期单位、按副本配置生成敌人、构建移动物理场景。
- 战斗循环编排：AI 前置推进与战斗推进收编进 LES 逻辑 tick（`BattleLoop`），转发 `IBattleScene.ApplyDecisions` 与 `Tick`，移动与施法经 `BattleScene` 与玩家共用同一权威入口；整帧事件日志在逻辑 tick 内编码后经传输层可靠通道外送。
- 玩家会话与连接密钥校验、断线重连；重连登记仅当房间已有同名会话才允许，杜绝冒用他人 playerId 绑单位；连接状态是会话本地数据（`PlayerSession`），不产生网络同步实体。
- 战斗输入回放录制：`BattleReplayRecorder` 在既有输入消费点旁路记录移动、施法与聚焦请求，三类记录共享同一帧轴，以首条记录的 tick 锚定绝对逻辑帧；内存存储并经快照导出，供回放工具消费；不改变权威校验。数据契约与编解码在共享层 `Entities.Replay`。
- 回放归档：房间销毁 `RemoveRoom` 或关服 `StopAll` 时编码回放快照并经 `IReplayStore` 契约归档，供大厅查询与下载；`InMemoryReplayStore` 实现位于本层 Replay 命名空间。

## 不负责

- 不实现大厅业务与传输层广播，广播契约 `ILobbyBroadcaster` 在 Server.Abstractions。
- 不为客户端结算：结算权威都在 BattleScene，客户端只有移动预测。
- 大厅线程不触碰 EntityManager，只做生命周期控制。
- 不承担回放执行：回放工具与观战消费端后置，录制器只负责内存存储与快照导出。

## 依赖项

- 共享层 Battle.Logic、Battle.Domain、Entities、GameConfig 与 Protocol；存储层经 `IGameStateStore`；契约层 Server.Abstractions。
