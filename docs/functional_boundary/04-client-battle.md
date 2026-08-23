# DungeonChessBattle.Client.Battle

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。

## 职责

- 管理 LES 实体生命周期与事件派发，统一暴露给 UI。
- 保存当前房间会话的战斗事件日志，只读暴露给 UI。
- 上送施放、聚焦与移动请求。
- 以与服务端同源的移动逻辑提供客户端本地预测。

## 边界外

- 不实现战斗结算、读条、伤害、冷却与仇恨，全部服务端权威。
- 不为 UI 实现业务，UI 只消费客户端战斗服务接口。
- 不含大厅阶段能力。

## 依赖

- Protocol、Entities、Battle.Logic、GameConfig。
