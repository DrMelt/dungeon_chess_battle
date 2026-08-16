# DungeonChessBattle.Client.Battle

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。

## 职责范围

- LES `ClientEntityManager` 生命周期与实体事件派发。
- 施放、聚焦目标与移动请求：施法与目标经可靠请求通道，移动经输入流提交。
- 客户端权威移动：按副本键构建与服务端同源的 `PhysicsMovementScene`，提供本地预测。
- 网络指标快照：`NetworkStatusSnapshot` 与统计型 `CountingNetPeer`。

## 不负责

- 不实现战斗结算、读条、伤害、冷却与仇恨，全部服务端权威。
- 不为 UI 实现业务，UI 只消费 `IClientBattleService` 接口。
- 不含大厅阶段能力。


## 依赖项

- Protocol、Entities、Battle.Logic、GameConfig。
