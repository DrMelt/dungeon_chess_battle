# DungeonChessBattle.Game.Mod.Shared

展示契约层：宿主实现、mod 调用的全部口——条目数据读写面与装配上下文，以及 mod 实现、宿主调用的表现契约。只依赖 `Game.Shared`，不依赖 `Game.Mod.Interface`。

## 职责

- 条目数据读面 `IDisplayRegistry`：四类条目展示数据按内容键读，mod 与宿主 UI 查同一张表。场景模板与图标纹理不经名，随条目数据以对象携带，mod 引自己包内的资源。
- 条目数据写面 `IModDisplayRuntime`：注册四类条目展示数据，同键后注册覆盖且未声明字段沿用被覆盖者。
- 装配上下文：入口初始化的第二参，携带 mod 归属与注册表；包内资源由 mod 自行按 `res://mods/{mod id}/` 前缀加载。
- 表现契约 `IRectRangeHint`：mod 侧提示场景实现，宿主按口驱动预览，宿主不认识场景的脚本类型与节点结构。

## 边界外

- 不含 mod 入口契约：`IModDisplayEntry` 在 Game.Mod.Interface。
- 不含注册表实现：只给读写口，注册表与装配上下文实现在 Game.Mod.Manager。
- 不含包内资源加载端口：mod 侧读自己包内文件不经宿主中转。
- 不含展示数据形状：四类展示数据在 Game.Shared。
- 不含装载与管理逻辑：扫描、启停、展示装配在 Game.Mod.Manager。

## 依赖

- Game.Shared。GodotSharp 随该链由 `Godot.NET.Sdk` 提供，与主工程同版本；本库不定义 `GodotObject` 子类，不产生脚本注册需求。
