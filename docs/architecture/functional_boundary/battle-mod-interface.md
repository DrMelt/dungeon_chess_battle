# DungeonChessBattle.Battle.Mod.Interface

mod 数据代码的引用锚点：只放 mod 要实现的接口，其余经本库的引用链传递可见。纯 .NET 类库，无引擎依赖，数据 mod 只需引用本库一个程序集。

## 职责

- 数据入口接口：mod 数据代码实现它，由宿主装载并调用，初始化时把内容与行为注册进装配面。本库只放这一个类型。

## 边界外

- 不含注册面：行为注册、内容注册与合成口在 Battle.Mod.Shared，由宿主实现、mod 调用。
- 不含行为 ID 常量：归 Battle.Shared。
- 不含 mod 包的扫描与门控：包布局、依赖顺序、启用集与内容指纹归 Battle.Mod.Manager。
- 不含程序集装载与入口执行：归 Battle.Mod.Manager。
- 不定义行为端口与内容类型本身：在 Battle.Shared。
- 不含展示面接口：在 Game.Mod.Interface 与 Game.Mod.Shared，仅客户端侧装载。

## 依赖

- Battle.Mod.Shared。
