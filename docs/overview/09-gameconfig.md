# DungeonChessBattle.GameConfig

单位与副本配置库，所属分组 Shared。纯 C# 配置数据库，零反射，编译期类型安全，服务端与客户端共用同一套配置。职责边界见 `functional_boundary/09`。

## 配置形态

- `GameConfigDB` 把 Buff、技能、单位、副本直接实例化为领域只读定义（`SkillDefinition` 族 / `BuffDefinition` / `RangeShape`），编译期类型安全，无运行时反射与热加载。
- 单位配置 `UnitConfig` 除数值外直接装配领域行为：`Intelligence`（`IUnitIntelligence` 无状态实例，多单位、多房间可共享）、`HateRule`、`Skills` 列表。
- 读取接口 `IGameConfigDB` 契约 + `GameConfigDB.Instance` 单例，供 Godot 脚本访问。

## 权威登记点

- `UnitRegistry`：ConfigKey ↔ UnitConfig 唯一注册点。服务端建模与控制器绑定校验、客户端 `UnitCatalog` 展示共享同一份配置，新增单位只登记一处。
- `DungeonRegistry`：DungeonKey ↔ DungeonConfig。服务端按房间选中副本生成敌人、指派玩家阵营，客户端按副本键选环境表现。
- 敌人生成以注册表权威配置键为准，`GetByConfig` 反查杜绝拼写错配。

## 副本配置构成

`DungeonConfig` 聚合：

- `PlayerCampOptions`：玩家阵营选项（选项键 → 实际阵营列表），客户端提交选项键，服务端权威解析，单位配置不含阵营。
- `Enemies`：敌人生成阵容（单位配置引用 + 数量 + 出生点参数）。
- `RelationsResolver`：阵营关系函数，敌我判定唯一来源。
- `Layout`：战场布局（边界 + 静态障碍矩形），服务端与客户端据此构建同源移动场景。

