# DungeonChessBattle.Battle.Client

房间战斗客户端，LiteNetLib + LiteEntitySystem 传输，所属分组 Client。管理 LES 实体，把服务端实体事件映射为 UI 可消费的接口事件。

## 职责

- 管理 LES 实体生命周期与事件派发，经 `IClientBattleSession` 契约暴露给上层。
- 保存当前房间会话的战斗事件日志，只读暴露给 UI。
- 上送施放、聚焦与移动请求。
- 把服务端下行的 SyncVar 读数回填进本地领域单位，作为展示与判定的唯一取数源。

## 边界外

- 不实现战斗结算、读条、伤害、冷却与仇恨，全部服务端权威。
- 不为 UI 实现业务，上层只消费 `IClientBattleSession`；连接生命周期不在该契约内，归客户端门面。
- 不含大厅阶段能力。

## 依赖

- Client.Shared、Battle.Entities、Battle.Logic、GameConfig。
