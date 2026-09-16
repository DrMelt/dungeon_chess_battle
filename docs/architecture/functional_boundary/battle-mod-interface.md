# DungeonChessBattle.Battle.Mod.Interface

mod 数据代码的引用锚点：只放 mod 要实现的接口，其余经本库的引用链传递可见。纯 .NET 类库，无引擎依赖，数据 mod 只需引用本库一个程序集。

## 职责

- 数据入口接口：mod 数据代码实现它，由宿主装载并调用，初始化时把内容定义注册进装配面。本库只放这一个类型。

## 边界外

- 不含注册面：内容注册口与引导上下文在 Battle.Mod.Shared，由宿主实现、mod 调用。
- 不含 mod 包的扫描与门控：包布局、依赖顺序、启用集与内容指纹归 Battle.Mod.Manager。
- 不含程序集装载与入口执行：归 Battle.Mod.Manager。
- 不定义行为端口与内容类型本身：端口与只读视图在 Battle.Shared，内容定义在 Battle.Config.Shared。
- 不含战斗运行时对象：单位权威实体与运行时状态在 Battle.Runtime.Shared，该库不发布，mod 编译期不可见。
- 不含展示面接口：在 Game.Mod.Interface、Game.Mod.Shared 与 Game.Display.Registry，仅客户端侧装载。

## 依赖

- Battle.Mod.Shared。
