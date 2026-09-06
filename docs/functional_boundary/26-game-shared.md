# DungeonChessBattle.Game.Shared

Godot 侧展示层共享契约库。定义"展示数据长什么样"、"展示代码入口长什么样"与"展示资源怎么加载"，不含任何实现。

存在的唯一理由：这些契约被三方共享——`Game` 的 `.tres` 资源类实现它、`Game.Mod` 实现注册面并装配它、展示代码 mod 的 `code_display/*.dll` 面向它注册，`Game` 的 UI 面向它查询。放 `Game` 会让 `Game.Mod` 与展示 mod 反向依赖主工程成环，放 `Game.Mod` 会让所有面板与编辑器资源绑架到 mod 子系统上。

## 职责

- 展示视图契约 `ISkillView` / `IBuffView` / `IDungeonView` / `IUnitView`：内置资源与 mod 展示代码共同的成员形状。未声明的字符串成员为空串、资源成员为 null，回退值由消费方决定。`IUnitView` 额外携带模型场景与主体配色（`ModelScene`/`BodyColor`），未配置为 null，消费方回落共享模板。
- 展示入口契约 `IModDisplayEntry` / `ModDisplayContext`：展示代码 mod 实现入口，在装配期把资源与视图注册进注册面；上下文携带 mod ID 与包内资源加载器。
- 展示注册面 `IModDisplayRuntime`：注册纹理与场景资源名、注册四类条目视图。注册什么由内容方定，宿主只把这个口递出去。
- 展示索引查询面 `IDisplayRegistry`：按身份键取视图、按资源名取纹理与场景。
- 资源加载端口 `IModResourceLoader`：契约在此、实现在 `Game.Mod`，使 mod 资源解析可被替身替换。
- mod 资产寻址值类型 `ModAssetKey`：mod ID + 包内相对路径。

## 边界外

- 不含装载与管理逻辑：mod 扫描、启停、展示装配、加载实现全在 `Game.Mod`（`functional_boundary/25`）。
- 不含注册表实现与合并语义：`DisplayRegistry` 在 `Game.Mod`，同键覆盖与字段级合并是它的行为，不是契约。
- 不含内容定义：技能键、BuffTypeId 等一律以 `string`/`ushort` 表达，强类型内容对象在 `Battle.Shared` 与 `GameConfig`，本库不自引。
- 不含可被 `.tres`/`.tscn` 引用的脚本类：那类资源基类与资源表必须留在 Godot 工程目录内（`res://` 路径写死在资源文件里），见 `functional_boundary/01`。

## 依赖

- GodotSharp：契约成员需要 `Texture2D` / `PackedScene` / `Color`。用 `Godot.NET.Sdk` 取，与主工程同版本；本库零项目引用。
