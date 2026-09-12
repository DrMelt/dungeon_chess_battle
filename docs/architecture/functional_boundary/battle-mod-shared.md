# DungeonChessBattle.Battle.Mod.Shared

数据面注册面定义层：宿主实现、mod 调用的口——内容注册口与引导上下文。纯 .NET 类库，只引 Battle.Shared。

## 职责

- 内容注册面：把技能/Buff/单位/副本定义注册进内容注册表，四类内容同构，以内容身份键为身份，同键后写覆盖。
- 引导上下文：数据入口初始化时拿到的唯一句柄，只提供内容定义注册。

## 边界外

- 不含 mod 要实现的接口：数据入口接口在 Battle.Mod.Interface，本库不引用它。
- 不含实现：内容注册表在 Battle.GameConfig，引导上下文实现与装配引导在 Battle.Mod.Manager。
- 不含装载与管理：扫描、清单、依赖排序与程序集装载在 Battle.Mod.Manager。

## 依赖

- Battle.Shared。mod 数据代码经 Battle.Mod.Interface 引用链传递可见。
