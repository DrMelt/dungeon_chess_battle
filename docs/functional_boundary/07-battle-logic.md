# DungeonChessBattle.Battle.Logic

战斗逻辑实现层，所属分组 Shared。以 `BattleScene` 实现战斗世界契约 `IBattleScene` 并编排战斗节拍，提供 Buff、仇恨、移动与战斗结算的具体逻辑，只面向领域接口，不依赖网络。AI 决策仅依赖查询视图 `IBattleSceneView`。

## 职责范围

- 战斗编排：`BattleScene.Tick(dt)` 统一推进阶段机、读条、冷却、Buff 与技能结算，产出领域事件流；`ApplyDecisions(dt)` 前置触发单位自治决策（`IBattleUnit.RunAI`），单位经注入的 `IAiExecutor` 回请求移动与施法，场景实现执行器并承担校验与日志。单位权威状态经 `IBattleUnit.RuntimeState` 读写，房间级阶段状态经 `IBattleRoom` 直接读写载体，场景只做推进、投影与结算。
- 事件编排：`BattleScene` 每帧经 `BattleEventLog` 收集结算产出的事件，处理开始清空、处理中只增追加；`HateDispatcher` 帧末统一消费事件流产出仇恨效果，`BattleScene` 按持有者路由到单位仇恨表落账。事件日志仅当帧有效。
- 战斗世界归属：`BattleScene` 持有竞技场移动场景，`AddUnit`/`RemoveUnit` 与空间演员注册同生命周期收敛；`MovementScene` 供实体层接线移动结算。
- Buff 创建与效果、施法校验与结算、伤害/治疗处理、仇恨事件分发与落账路由、位移解析与物理移动场景。

## 不负责

- 不依赖网络与实体载体，不实现序列化与广播。
- 不含客户端表现与输入采集。
- 不负责网络同步，权威状态由战斗世界单向投影回载体。


## 依赖项

- Battle.Domain。
