# DungeonChessBattle.Server.Battle

战斗房间服务层，所属分组 Server。每个房间拥有独立的 LES 实体服务器与战斗引擎，运行于独立线程。

## 职责范围

- 单房间 LES 服务器：独立 `NetManager`、`ServerEntityManager` 与 `BattleEngine`，独立后台线程 50Hz 固定步长驱动。
- 房间生命周期：创建/查找/销毁、端口池分配回收、空房清理队列。
- 初始化与登场：迁移准备期单位、按副本配置生成敌人、构建移动物理场景。
- 敌人大脑编排：存活调度与决策映射，决策入参为按帧组装的只读战场视图 `IBattleScene`，决策器直接取单位配置持有的领域智能实例，阵营关系按副本注入；施法与移动打断与玩家侧共用 BattleEngine 权威入口。
- 玩家会话与连接密钥校验、断线重连。

## 不负责

- 不实现大厅业务与传输层广播。
- 不为客户端结算：结算权威都在 BattleEngine，客户端只有移动预测。
- 大厅线程不触碰 EntityManager，只做生命周期控制。


## 依赖项

- 共享层 Battle.Logic、Battle.Domain、Entities、GameConfig 与 Protocol；存储层经 `IGameStateStore`。
