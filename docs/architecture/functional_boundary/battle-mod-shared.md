# DungeonChessBattle.Battle.Mod.Shared

数据面注册接口定义层：宿主实现、mod 调用的内容注册接口与引导上下文。纯托管类库。

## 职责

- 内容注册接口：把技能、Buff、单位、副本定义注册进内容注册表，以内容身份键为身份，同键后写覆盖；单位另有玩家可选注册接口，一次注册同入单位表与可选名册。
- 引导上下文：数据入口初始化时拿到的唯一句柄，提供内容定义注册与日志通道；mod 据此按自身类别名记录运行期诊断。

## 边界外

- 不含 mod 要实现的接口：数据入口接口在 Battle.Mod.Interface，本库不引用它。
- 不含实现：内容注册表在 Battle.Config.Registry，引导上下文实现与装配引导在 Battle.Mod.Manager。
- 不含装载与管理：扫描、清单、依赖排序与程序集装载在 Battle.Mod.Manager。

## 依赖

- Battle.Config.Shared。
