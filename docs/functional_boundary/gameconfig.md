# DungeonChessBattle.Battle.GameConfig

内容装配宿主库。单位/副本等全部内容由 mod 程序集提供，本库负责装配、注册表与行为目录，不持有任何内容定义。纯 C#，服务端与客户端共用同一套装配结果。

## 职责

- 内容装配根 `GameContentHost`：创建 `ContentSetRegistry` 与 `BehaviorCatalog`，内容修订号退化为引擎固定常量，内容指纹由启用 mod 计算。
- 内容装配 `ContentBootstrapper`：只消费 `Battle.Mod.Manager.ModLoader` 的扫描结果装配内容，不自行扫描；逐 mod 装载数据代码入口，内容和行为全部注册进空注册表。
- `ContentSetRegistry`：Buff/Skill/Unit/Dungeon 领域定义的注册表与各向索引，mod 按装载顺序同键覆盖；实现只读视图 `IContentRegistryView`，展示面与只读消费方经它查询。
- `BehaviorCatalog`：行为目录（技能/Buff 效果、敌人决策、仇恨规则、阵营关系）容器，行为由 mod 数据代码注册，引擎不内置任何行为。
- 引导上下文 `ModBootstrapContext`：`IModEntry` 的实参，行为注册与取用转发 `BehaviorCatalog`，内容注册转发 `ContentSetRegistry`。
- `UnitRegistry`/`DungeonRegistry`：单位与副本的权威登记点，从注册表构建，配置键与配置模型映射。
- `BattleUnitFactory`：以单位配置为蓝图装配战斗单位领域实体，无状态。

## 边界外

- 不做权威裁定：施法与目标可行性判据在 Battle.Logic，本层经 `IBattleSceneView.CanCast` 取结论。
- 不做战斗编排：节拍推进、事件顺序、意图消费与状态写回均在 Battle.Logic。
- 不提供内容：无内置单位/技能/Buff/副本，零 mod 环境注册表为空。
- 不含运行时反射：内容为编译期拼装的定义对象，无序列化反转环节。

## 依赖

- Battle.Shared；Battle.Mod.Manager、Battle.Mod.Interface 与 Battle.Mod.Shared。不依赖 Battle.Logic——配置持有的行为实例经 Shared 端口被战斗世界调用，引用方向单向朝下。
