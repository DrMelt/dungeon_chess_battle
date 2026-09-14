# DungeonChessBattle.Battle.Runtime.Shared

战斗运行时对象层：开局后创建、随战斗推进改写的对象，以及它们的装配与展示形状。纯 .NET 类库，只引 Battle.Shared 与 Battle.Config.Shared。

## 职责

- 单位领域实体：单位权威状态、基础数值与技能定义的持有者，读写能力只在该类，不依赖网络与框架类型。
- 运行时状态容器：读条目标、Buff 与冷却列表、仇恨账本，以及 Buff 实例与效果策略的运行时配对。
- 装配：以单位配置为蓝图装配战斗单位，装配期直拷配置字段，不查注册表，无状态可复用。
- 按帧意图与移动解算口：施法与移动意图的消费载体、批量位移解算口，意图经战斗世界唯一写入面消费后作废。
- 展示视图：单位与 Buff 的展示只读形状，在线与回放共用。
- 回放门控指纹：引擎结算逻辑修订号，递增义务在 Battle.Logic。

## 边界外

- 不含跨端数据形状、身份键、行为端口与只读视图，归 Battle.Shared；不含静态配置数据，归 Battle.Config.Shared。
- 不含战斗编排、Buff 节拍、位移解算、仇恨分发与施法校验，均在 Battle.Logic。
- 不含网络载体与传输，也不做序列化与网络载体：同步实体在 Battle.Entities。
- 不对 mod 发布：mod 的编译期上界止于 Battle.Config.Shared，看不到权威实体写面。

## 依赖

- Battle.Shared、Battle.Config.Shared。
