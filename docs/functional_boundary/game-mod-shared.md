# DungeonChessBattle.Game.Mod.Shared

展示注册器定义层：宿主实现、mod 调用的全部口——写面、读面、包内资源加载端口与装配上下文。只依赖 `Game.Shared`，不依赖 `Game.Mod.Interface`。

## 职责

- 写面 `IModDisplayRuntime`：注册纹理与场景资源名、注册四类条目展示数据。注册什么由内容方定，宿主只把这个口递出去。
- 读面 `IDisplayRegistry`：按技能键/BuffTypeId/配置键/副本键取展示数据，按资源名取纹理与场景对象。mod 据此改写已声明条目而不必先验其内容，宿主 UI 与 mod 查同一张表。
- 加载端口 `IModResourceLoader`：按 `ModAssetKey` 把 mod 包内图片、场景与展示数据 `.tres` 解析成 Godot 对象，契约与寻址键同在本库、实现在 Game.Mod.Manager。寻址键只是包内路径寻址、不是展示形状，不随展示形状进 Game.Shared。
- 装配上下文 `ModDisplayContext`：入口 `Initialize` 的第二参，携带 mod ID、包内加载器与注册表读面。

## 边界外

- 不含 mod 要实现的接口：`IModDisplayEntry` 在 Game.Mod.Interface，本库不引用它。
- 不含实现与合并语义：`DisplayRegistry`、`ModResourceLoader`、同键覆盖与字段级合并在 Game.Mod.Manager。
- 不含展示数据形状与引擎预置资源名：四个 `*Display`、`DisplayAssetIds` 在 Game.Shared；寻址形状 `ModAssetKey` 随加载端口在本库。
- 不含装载与管理逻辑：扫描、启停、展示装配在 Game.Mod.Manager。

## 依赖

- Game.Shared。GodotSharp 随该链由 `Godot.NET.Sdk` 提供，与主工程同版本；本库不定义 `GodotObject` 子类，不产生脚本注册需求。
