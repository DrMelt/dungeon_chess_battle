# DungeonChessBattle.Game.Mod.Shared

展示注册器定义层：宿主实现、mod 调用的全部口——写面、读面、包内资源加载端口与装配上下文。只依赖 `Game.Shared`，不依赖 `Game.Mod.Interface`。

## 职责

- 写面：注册纹理/场景资源名与四类条目展示数据。
- 读面：按内容键取展示数据、按资源名取纹理与场景对象，mod 与宿主 UI 查同一张表。
- 加载端口：按包内寻址键把图片、场景与展示数据解析成 Godot 对象。
- 装配上下文：入口初始化的第二参，携带 mod 归属、包内加载器与注册表读面。

## 边界外

- 不含 mod 要实现的接口：`IModDisplayEntry` 在 Game.Mod.Interface。
- 不含注册器实现：只给写面与读面，注册器、加载器与装配上下文实现在 Game.Mod.Manager。
- 不含展示数据形状与引擎预置资源名：四类展示数据在 Game.Shared。
- 不含装载与管理逻辑：扫描、启停、展示装配在 Game.Mod.Manager。

## 依赖

- Game.Shared。GodotSharp 随该链由 `Godot.NET.Sdk` 提供，与主工程同版本；本库不定义 `GodotObject` 子类，不产生脚本注册需求。
