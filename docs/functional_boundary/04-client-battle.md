# DungeonChessBattle.Client.Battle

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。

## 职责范围

- LES `ClientEntityManager` 生命周期与实体事件派发；可靠消息帧解析与战斗事件日志解码，经 `BattleEventsReceived` 统一暴露给 UI。
- 事件日志保存：当前房间会话收到的全部战斗事件（含接收时刻）经 `BattleEventLogStore` 保存，`GetEventLog()` 只读暴露给 UI 增量消费与历史回填，`GetEventLogVersion()` 版本号随会话重置自增供 UI 识别；断线/重连/离开房间时清空。
- 施放、聚焦目标与移动请求：施法与目标经可靠请求通道，移动经输入流提交。
- 客户端权威移动：构建与服务端同源的 `PhysicsMovementScene`，提供本地预测。
- 网络指标快照：`NetworkStatusSnapshot` 与统计型 `CountingNetPeer`。

## 不负责

- 不实现战斗结算、读条、伤害、冷却与仇恨，全部服务端权威。
- 不为 UI 实现业务，UI 只消费 `IClientBattleService` 接口。
- 不含大厅阶段能力。


## 依赖项

- Protocol、Entities、Battle.Logic、GameConfig。
