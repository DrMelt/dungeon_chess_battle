# DungeonChessBattle.Battle.Mod.Shared

数据面注册面定义层：宿主实现、mod 调用的口——内容注册口与引导上下文。纯 .NET 类库，引 Battle.Config.Shared，并因引导上下文交出日志工厂而引日志抽象。

## 职责

- 内容注册面：把技能/Buff/单位/副本定义注册进内容注册表，四类内容同构，以内容身份键为身份，同键后写覆盖。
- 引导上下文：数据入口初始化时拿到的唯一句柄，提供内容定义注册与日志通道；mod 据此按自身类别名记录运行期诊断。

## 边界外

- 不含 mod 要实现的接口：数据入口接口在 Battle.Mod.Interface，本库不引用它。
- 不含实现：内容注册表在 Battle.Config.Registry，引导上下文实现与装配引导在 Battle.Mod.Manager。
- 不含装载与管理：扫描、清单、依赖排序与程序集装载在 Battle.Mod.Manager。

## 依赖

- Battle.Config.Shared。
