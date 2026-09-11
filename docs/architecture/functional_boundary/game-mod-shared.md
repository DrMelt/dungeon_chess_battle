# DungeonChessBattle.Game.Mod.Shared

展示注册表定义层：宿主实现、mod 调用的全部口——场景资源口、条目数据写面与装配上下文。只依赖 `Game.Shared`，不依赖 `Game.Mod.Interface`。

## 职责

- 场景资源口 `IDisplayRegistry`：场景资源名的注册与解析，名是全局命名空间、同名后注册覆盖；四类条目展示数据也经本口按内容键读，mod 与宿主 UI 查同一张表。图标纹理不经名，随条目数据以对象携带。
- 条目数据写面 `IModDisplayRuntime`：注册四类条目展示数据，同键后注册覆盖且未声明字段沿用被覆盖者。
- 装配上下文：入口初始化的第二参，携带 mod 归属与注册表；包内资源由 mod 自行按 `res://mods/{mod id}/` 前缀加载。

## 边界外

- 不含 mod 要实现的接口：`IModDisplayEntry` 在 Game.Mod.Interface。
- 不含注册表实现：只给场景资源口与条目写面，注册表与装配上下文实现在 Game.Mod.Manager。
- 不含包内资源加载端口：mod 侧读自己包内文件不经宿主中转。
- 不含展示数据形状与引擎预置资源名：四类展示数据在 Game.Shared。
- 不含装载与管理逻辑：扫描、启停、展示装配在 Game.Mod.Manager。

## 依赖

- Game.Shared。GodotSharp 随该链由 `Godot.NET.Sdk` 提供，与主工程同版本；本库不定义 `GodotObject` 子类，不产生脚本注册需求。
