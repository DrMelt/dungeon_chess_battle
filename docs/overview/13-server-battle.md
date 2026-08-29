# DungeonChessBattle.Battle.Server

战斗房间服务层，所属分组 Server。每个房间拥有独立的 LES 实体服务器与战斗世界，运行于独立线程。职责边界见 `functional_boundary/13`。

## 单房间模型与线程所有权

- 每个战斗房间 = 独立后台线程 + 独立 `NetManager` + 独立 `ServerEntityManager` + 独立 `BattleScene` + 独立端口（50 FPS，sendRate 与帧率一致），实现实体同步物理隔离。
- 线程所有权严格：EntityManager 的全部操作（初始化、建实体、RPC、Update）只在房间线程；大厅线程只做生命周期控制（启动、等待初始化信号、停止）。
- `BattleRoomManager` 管理端口池（从 `FirstRoomPort` 递增分配，销毁回收）与房间注册表；实现 `IBattleRoomManager` 契约，对外只暴露端口等原语。

## 首帧初始化

房间线程启动后 `InitializeFromStore` 依次：

1. 创建 `BattleRoomEntity` 注入权威副本键，装配 `BattleStateSynchronizer`。
2. 从 Store 迁移准备期单位：按副本配置解析玩家阵营、同阵营错开出出生点、建 Pawn、注入战斗系数与技能。
3. 按副本配置生成敌人（配置键经注册表反查，注入智能决策器）。
4. 创建回放录制器，注册 `BattleLoop` LocalSingleton。
5. `StartBattle` 立即进入 Running（阶段先于客户端连入写定，技能请求不会被阶段校验拒绝）。

## 战斗循环

- `BattleLoop.Update` = `ApplyDecisions`（AI 前置，输入本帧生效）；`LateUpdate` = `Tick`（推进）→ 状态同步 → 整帧事件外送。LocalSingleton 不参与预测回滚。
- 事件外送：Tick 返回的领域事件经 `BattleEventCoder` 编码 → `ReliableMessageFrame` 打包 → `SendReliableOrdered` 逐在线会话广播。空帧不发，断线期间不补发。

## 玩家会话与断线重连

- `PlayerSession` 聚合 playerId → PeerId/NetPlayer/Controller/Pawn，连接状态是会话本地数据不产生网络实体。
- 连接密钥即 playerId：`OnConnectionRequest` 校验服务器密钥或 Store 房间成员白名单；同一 playerId 已有活跃连接时关闭旧连接接受新连接。
- 断线仅清会话连接状态，单位与战斗状态保留，玩家可随时凭成员身份重连；重连重建 NetPlayer 并重新绑定控制器。
- 重连登记（大厅层 `RegisterPlayer`）仅当房间已有同名会话才允许，杜绝冒用他人 playerId 绑单位。

## 空房清理与回放归档

- 全部活跃连接断开且初始化完成 → `RoomEmpty` 事件 → 投递队列 → 大厅后台清理循环 `ProcessPendingRoomCleanups` → `RemoveRoom`（停止线程 → 回收端口 → 归档回放）。
- 回放录制：`BattleReplayRecorder` 在既有输入消费点旁路记录移动/施法/聚焦，三类记录共享同一帧轴（首条 tick 锚定绝对逻辑帧，规避 ushort 回绕），上限 100 万条。
- 归档：房间销毁或关服时编码 `ReplayRecordSnapshot` 经 `IReplayStore` 写入实现层 `InMemoryReplayStore`（保留最近 256 场），供大厅查询与 HTTP 下载。

