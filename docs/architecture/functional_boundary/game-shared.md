# DungeonChessBattle.Game.Shared

mod 与宿主共用的展示形状层：注册表收发的展示数据。纯形状，不含接口与实现；只依赖 Battle.Shared 的强类型键。

## 职责

- 四类条目展示数据：技能/Buff/单位/副本各一种，一类型一文件：未声明成员为空串或 null，回退由消费方决定；场景模板随展示数据以对象携带，技能带范围提示场景、副本带环境场景、单位带模型场景与主体配色。四类键都是内容身份强类型：技能 `SkillKeyId`、Buff `BuffTypeId`、单位 `UnitConfigKey`、副本 `DungeonKeyId`。

## 边界外

- 不放接口：注册表口在 Game.Mod.Shared，mod 入口在 Game.Mod.Interface。
- 不放只有宿主用的形状。
- 不放可被 `.tres`/`.tscn` 引用的脚本类与 `res://` 路径，见 `functional_boundary/game`。
- 不放持有他库类型的形状与 mod 包管理结构；唯一例外是内容身份键，四类键取自 Battle.Shared。

## 依赖

- Battle.Shared 的强类型键，此外无项目引用。被 Game 与 Game.Mod.Shared 引用，经引用链进 mod 的传递闭包，mod 编译期因此一并带出零项目引用的 Battle.Shared；GodotSharp 成员是引擎资源类型，与主工程同用 `Godot.NET.Sdk`，本库不定义 `GodotObject` 子类，不产生脚本注册需求。
