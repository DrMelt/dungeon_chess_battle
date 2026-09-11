# DungeonChessBattle.Battle.Mod.Shared

数据面注册面定义层：宿主实现、mod 调用的口——行为注册面 `IModRuntime`、内容注册面 `IModContentRuntime` 与合成口 `IModBootstrapContext`。纯 .NET 类库，只引 Battle.Shared。

## 职责

- 行为注册面 `IModRuntime`：行为按字符串 ID 注册进目录并按 ID 取实例，供构造内容定义填入行为。
- 内容注册面 `IModContentRuntime`：把技能/Buff/单位/副本定义对象注册进内容注册表，四类内容同构，各以内容身份键（`SkillKeyId` / `BuffTypeId` / `UnitConfigKey` / `DungeonKeyId`）为身份，同键后写覆盖。
- 合成口 `IModBootstrapContext`：两面的合成句柄，作入口初始化的唯一实参。

## 边界外

- 不含 mod 要实现的接口：入口契约 `IModEntry` 在 Battle.Mod.Interface，本库不引用它。
- 不含行为 ID 常量：归 Battle.Shared。
- 不含实现：行为目录与内容注册表在 Battle.GameConfig，装配上下文实现与装配引导在 Battle.Mod.Manager。
- 不含装载与管理：扫描、清单、依赖排序与程序集装载在 Battle.Mod.Manager。

## 依赖

- Battle.Shared。mod 数据代码经 `Battle.Mod.Interface` 引用链传递可见。
