# DungeonChessBattle.Game.Mod.Interface

mod 展示代码的引用锚点：只放 mod 要实现的接口，其余经本库的引用链传递可见。展示代码程序集编译时只需引用本程序集。

## 职责

- 展示入口接口：展示代码 mod 实现它，由宿主装载并调用，把展示数据注册进条目写面。本库只放这一个类型。

## 边界外

- 不含注册表定义：条目数据读写面、装配上下文与表现接口在 Game.Mod.Shared。
- 不含展示数据：四类展示数据在 Game.Shared。
- 不含内容定义：内容身份键与内容对象在 Battle.Shared 与 GameConfig。
- 不含装载与管理逻辑：mod 扫描、启停、展示装配、加载实现在 Game.Mod.Manager。

## 依赖

- Game.Mod.Shared。引擎引用随该链由引擎 SDK 提供，与主工程同版本。
- 宿主不引用本库：引用锚点对主工程是空依赖。
