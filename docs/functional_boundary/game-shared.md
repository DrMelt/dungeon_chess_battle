# DungeonChessBattle.Game.Shared

mod 与宿主共用的展示形状层：注册器收发的展示数据、宿主登记的引擎预置资源名。纯形状，零项目引用，不含接口与实现。

## 职责

- 四类条目展示数据 `SkillDisplay` / `BuffDisplay` / `UnitDisplay` / `DungeonDisplay`，一类型一文件：未声明的字符串成员为空串、资源成员为 null，回退值由消费方决定，故同键后写覆盖按此语义做字段级合并，见 `Game.Mod.Manager`；`UnitDisplay` 额外携带模型场景与主体配色，未配置回落共享模板。
- 引擎预置资源名 `DisplayAssetIds`：宿主登记引擎预置的特效、范围提示与环境场景，mod 按名引用，不必硬编码字符串；`res://` 路径映射留在主工程 `BuiltinDisplayAssets`。

## 边界外

- 不放接口：注册器口在 Game.Mod.Shared，mod 要实现的入口在 Game.Mod.Interface。
- 不放只有宿主用的形状：那类 DTO 留在 Game。
- 不放加载端口的寻址形状：`ModAssetKey` 随 `IModResourceLoader` 在 Game.Mod.Shared。
- 不放可被 `.tres`/`.tscn` 引用的脚本类与任何 `res://` 路径：那类绑定必须留在 Godot 工程目录内，见 `functional_boundary/game`。
- 不放持有他库类型的形状：`ReplayPlayableResult` 携带 `ReplayRecording`，下沉会把回放模型拖进 mod 的引用闭包。
- 不放 mod 包管理结构：manifest、`LoadedMod`、启用集与指纹在 Battle.Mod.Manager。

## 依赖

- 无项目引用。被 Game 与 Game.Mod.Shared 引用，经引用链进 mod 的传递闭包；GodotSharp 成员是 `Texture2D` / `PackedScene` / `Color`，与主工程同用 `Godot.NET.Sdk`，本库不定义 `GodotObject` 子类，不产生脚本注册需求。
