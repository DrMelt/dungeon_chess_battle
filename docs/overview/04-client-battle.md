# DungeonChessBattle.Client.Battle

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。职责边界见 `functional_boundary/04`。

## 网络栈与实体管理

- 连接建立时创建 `ClientEntityManager`（包头 0xDC），用 `CountingNetPeer` 装饰 LES peer 采集出站流量；订阅 `BattleRoomEntity`、`UnitPawn`、`UnitController` 三类实体的构造事件（`callOnExisting: true` 补拉已同步实体）。
- 收到控制器实体即视为本地控制器（OnlyForOwner 分发，不依赖 `IsLocalControlled` 构造时序），用于输入提交与施法请求。

## 帧处理

`UpdateAfterPollEvents` 每帧依次：

1. 按房间副本键构建 `PhysicsMovementScene`（与服务端同源布局，用于客户端权威移动预测）；副本键未同步时返回 null，移动按自由移动回退。
2. `EntityManager.Update()` 驱动 LES 实体同步与预测回滚。
3. 每秒流量统计结算。
4. 轮询 `BattleRoomEntity` 阶段变化并触发 `BattlePhaseChanged`（LES 无公开 Changed 事件）。

## 收包分流

- 先经 `ReliableMessageFrame` 识别可靠消息帧（0xDC + 0x10 类型头）：解码 `ReliableBattleEventLog` → `BattleEventCoder` 逐条解码为领域事件 → 存入 `BattleEventLogStore`（含接收时刻）并触发 `BattleEventsReceived`。连接内可靠有序，断线期间事件不补发。
- 其余 0xDC 帧交 LES 反序列化。

## 事件日志仓库

- `BattleEventLogStore` 保存当前房间会话全部事件，`GetEventLog()` 只读暴露、`GetEventLogVersion()` 版本号在会话重置（断线/重连/离开）时自增，UI 据此做增量消费与历史回填。

## 确定性移动预测

- 每个 `UnitPawn` 注入 `MovementResolver.Move` + 本地物理场景，与服务端注入同一实现：客户端本地预测即时反馈消除 RTT 卡顿，服务端权威经 LES 回滚重放自动纠偏。

