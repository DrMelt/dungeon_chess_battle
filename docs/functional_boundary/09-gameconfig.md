# DungeonChessBattle.Battle.GameConfig

单位与副本内容配置库。配置数据以代码程序集与内置代码注册进 `ContentSetRegistry`（`overview/mod`），本库承担内容侧逻辑实现与领域定义组装。纯 C#，编译期类型安全，服务端与客户端共用同一套装配结果。

## 职责

- 内容装配根 `GameContentHost`：创建 `ContentSetRegistry` 并注册内置基座（`BuiltInContent`），是全部配置的唯一入口。
- `ContentSetRegistry`：Buff/Skill/Unit/Dungeon 领域定义的注册表与各向索引；内置基座先注册、mod 经引导上下文后注册同键覆盖，行为经 `BehaviorCatalog` 实例化注入定义对象。
- 引导上下文 `ModBootstrapContext`：`IModEntry` 的实参，行为注册与取用转发 `BehaviorCatalog`，内容注册转发 `ContentSetRegistry`。
- `BehaviorCatalog`：行为目录（技能/Buff 效果、敌人决策、仇恨规则、阵营关系），内置行为按 `BehaviorIds` 注册，mod 代码经 `IModEntry` 增补或覆盖。
- 内容侧逻辑实现：技能与 Buff 效果策略（`ISkillEffect`/`IBuffEffect`）、伤害与治疗公式（`DamageProcessor`/`HealProcessor`）、默认敌人决策（`EnemyIntelligence`），全部在 `BehaviorCatalog` 注册为无状态可共享实例。
- 单位与副本的权威登记点：`UnitRegistry`/`DungeonRegistry` 从 `ContentSetRegistry` 构建，配置键与配置模型映射。
- 默认副本键与配置读取契约。
- 修订号递增义务在内容侧：单位数值、技能与 Buff 效果、伤害治疗公式、敌人决策算法、仇恨规则选型、阵营与副本布局任一变化，都必须递增 `BuiltInContent.BuiltInRevision`；用户 mod 内容经指纹联动进 `GameConfigDB.DataRevision`。漏递增不报错，只会让旧录像在同一份输入下重跑出不同结果而门控看不出差别。

## 边界外

- 不做权威裁定：施法与目标可行性判据在 Battle.Logic，本层经 `IBattleSceneView.CanCast` 取结论，不持有判据。
- 不做战斗编排：节拍推进、事件顺序、意图消费与状态写回均在 Battle.Logic。
- 不越过登记点，新增单位/副本必须经内容管线登记。
- 不含运行时反射：内容装载边界例外（mod 代码 ALC 装载属于 Mod 域，见 `overview/mod`）；内容为编译期拼装的定义对象，无序列化反转环节。

## 依赖

- Battle.Shared；Battle.Mod（内容装载契约与引导上下文类型）。不依赖 Battle.Logic：配置持有的行为实例经 Shared 端口被战斗世界调用，引用方向单向朝下。
