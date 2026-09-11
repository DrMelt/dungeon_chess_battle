# DungeonChessBattle.Game.Shared

mod 与宿主共用的展示形状层：注册表收发的展示数据、宿主登记的引擎预置资源名。纯形状，零项目引用，不含接口与实现。

## 职责

- 四类条目展示数据：技能/Buff/单位/副本各一种，一类型一文件：未声明成员为空串或 null，回退由消费方决定；单位展示携带模型场景与主体配色。
- 引擎预置资源名：宿主登记的引擎预置资源名，mod 按名引用；`res://` 路径映射留在主工程。

## 边界外

- 不放接口：注册表口在 Game.Mod.Shared，mod 入口在 Game.Mod.Interface。
- 不放只有宿主用的形状。
- 不放可被 `.tres`/`.tscn` 引用的脚本类与 `res://` 路径，见 `functional_boundary/game`。
- 不放持有他库类型的形状与 mod 包管理结构。

## 依赖

- 无项目引用。被 Game 与 Game.Mod.Shared 引用，经引用链进 mod 的传递闭包；GodotSharp 成员是引擎资源类型，与主工程同用 `Godot.NET.Sdk`，本库不定义 `GodotObject` 子类，不产生脚本注册需求。
