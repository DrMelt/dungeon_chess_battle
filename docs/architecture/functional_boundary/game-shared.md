# DungeonChessBattle.Game.Shared

mod 与宿主共用的展示形状层：注册表收发的展示数据。

## 职责

- 四类条目展示数据：技能、Buff、单位、副本各一种，未声明字段取空值；展示用名称由展示数据自身回退内容身份键，其余字段回退由消费方决定；场景资源随展示数据以对象携带。

## 边界外

- 不放接口：注册表读写接口在 Game.Display.Registry，装配上下文与表现接口在 Game.Mod.Shared，mod 入口在 Game.Mod.Interface。
- 不放只有宿主用的形状。
- 不放可被引擎资源文件引用的脚本类与资源路径，见 [game](game.md)。
- 不放持有他库类型的形状与 mod 包管理结构；唯一例外是内容身份键，取自 Battle.Shared。

## 依赖

- Battle.Shared。
