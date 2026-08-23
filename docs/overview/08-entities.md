# DungeonChessBattle.Entities

LES 网络实体层，所属分组 Shared。服务端与客户端共用的实体类型、类型注册表与协议载荷。职责边界见 `functional_boundary/08`。

## 实体族

- `BattleRoomEntity`：房间级战斗状态投影载体。承载阶段、是否结束、开始时刻与副本键；禁止在 OnConstructed 重置同步字段（客户端先应用初始同步再执行 OnConstructed，重置会丢字段）。
- `UnitPawn`：单位 SyncVar 载体，实现只读视图 `IBattleUnitView`。生命、读条、冷却截止 tick、Buff/仇恨列表、阵营、死亡状态等均为网络同步字段；技能定义、智能决策器、移动管线为装配期本地写入不参与同步。
- `UnitController`：`HumanControllerLogic` 输入控制器，`SubmitInput` 写入待发缓冲、`SendCastSkillRequest` 走可靠请求通道；服务端 `BeforeControlledUpdate` 每 tick 把当前输入转发给受控 Pawn。

## 载体适配

- `IBattleUnitView` 只读适配：`UnitPawn` 投影 SyncVar 组装结算快照与冷却推算，供客户端技能预拦与展示；领域权威结算由战斗世界持 `BattleUnit` 完成，本适配不参与。
- 同步写回仅经 `IBattleProjector` 投影器，服务端调用。

## 类型注册表

- `EntityTypesRegistry`：枚举注册顺序服务端/客户端必须完全一致；静态构造注册自定义字段类型 `Vector2`（含插值器）。LES 对未注册字段类型静默剔除，缺失注册会导致字段不参与同步。

## 移动与输入

- `UnitPawn.Update` 执行确定性位移：调用注入的 `MoveResolver`（Logic 层纯函数），客户端预测与服务端权威同一实现，LES 回滚重放自动纠偏。
- 输入包 `UnitInputPacket` 为扁平顺序布局结构，只承载移动状态；技能/聚焦等一次性事件走可靠请求。

## 协议载荷

- 战斗事件编解码：`BattleEventCoder` 领域事件 ↔ `SyncBattleEvent` 双向映射，tag 与槽位语义唯一权威；解码遇未知 tag 返回 null 向前兼容。
- 可靠消息帧：`ReliableMessageFrame`（0xDC + 0x10 帧头）+ `ReliableBattleEventLog` 消息体，服务端外送与客户端解析共用。

