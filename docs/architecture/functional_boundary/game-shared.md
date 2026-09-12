# DungeonChessBattle.Game.Shared

mod 与宿主共用的展示形状层：注册表收发的展示数据。纯形状，不含接口与实现；只依赖 Battle.Shared 的强类型键。

## 职责

- 四类条目展示数据：技能、Buff、单位、副本各一种，未声明字段为空值，回退由消费方决定；场景资源随展示数据以对象携带。内容身份键为限长强类型值对象。

## 边界外

- 不放接口：注册表口在 Game.Mod.Shared，mod 入口在 Game.Mod.Interface。
- 不放只有宿主用的形状。
- 不放可被引擎资源文件引用的脚本类与资源路径，见 [game](game.md)。
- 不放持有他库类型的形状与 mod 包管理结构；唯一例外是内容身份键，取自 Battle.Shared。

## 依赖

- Battle.Shared 的强类型键，此外无项目引用。被 Game 与 Game.Mod.Shared 引用，经引用链进 mod 的传递闭包，mod 编译期因此一并带出零项目引用的 Battle.Shared；引擎资源类型由引擎 SDK 提供，与主工程同版本，本库不定义引擎对象子类，不产生脚本注册需求。
