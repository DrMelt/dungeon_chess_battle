# DungeonChessBattle.Battle.GameConfig

单位与副本内容配置库。配置数据以代码程序集与内置代码注册进 `ContentSetRegistry`，本库承担内容侧逻辑实现与领域定义组装。纯 C#，编译期类型安全，服务端与客户端共用同一套装配结果。

## 职责

- 内容装配根 `GameContentHost`：创建 `ContentSetRegistry` 并注册内置基座（`BuiltInContent`），是全部配置的唯一入口。
- 内容装配 `ContentBootstrapper`：只消费 `Battle.Mod.Manager.ModLoader` 的扫描结果装配内容，不自行扫描。
- `ContentSetRegistry`：Buff/Skill/Unit/Dungeon 领域定义的注册表与各向索引，内置基座先注册、mod 后注册同键覆盖；实现只读视图 `IContentRegistryView`，展示面与只读消费方经它查询。
- 引导上下文 `ModBootstrapContext`：`IModEntry` 的实参，行为注册与取用转发 `BehaviorCatalog`，内容注册转发 `ContentSetRegistry`。
- `BehaviorCatalog`：行为目录（技能/Buff 效果、敌人决策、仇恨规则、阵营关系），内置行为按 `BehaviorIds` 注册，mod 代码经 `IModEntry` 增补或覆盖。
- 内容侧逻辑实现：技能与 Buff 效果策略、伤害与治疗公式、默认敌人决策，注册为无状态可共享实例。
- 单位与副本的权威登记点：`UnitRegistry`/`DungeonRegistry` 从注册表构建，配置键与配置模型映射。
- 默认副本键与数据修订号读取：消费方经注册表单点取用，无独立静态配置门面。
- 内容侧修订号递增义务：单位数值、技能与 Buff 效果、伤害治疗公式、敌人决策算法、仇恨规则选型、阵营与副本布局任一变化，必须递增 `BuiltInContent.BuiltInRevision`；用户 mod 内容经指纹联动进 `DataRevision`。

## 边界外

- 不做权威裁定：施法与目标可行性判据在 Battle.Logic，本层经 `IBattleSceneView.CanCast` 取结论。
- 不做战斗编排：节拍推进、事件顺序、意图消费与状态写回均在 Battle.Logic。
- 不越过登记点，新增单位/副本必须经内容管线登记。
- 不含运行时反射：内容为编译期拼装的定义对象，无序列化反转环节。

## 依赖

- Battle.Shared；Battle.Mod.Manager、Battle.Mod.Interface 与 Battle.Mod.Shared。不依赖 Battle.Logic——配置持有的行为实例经 Shared 端口被战斗世界调用，引用方向单向朝下。
