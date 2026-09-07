# DungeonChessBattle.Game.Mod.Manager

Godot 端 mod 子系统：管 mod 包、执行展示装配全过程、实现展示注册器与包内资源加载、提供统一获取入口。只被 Game 引用，服务端程序集不携带。与数据面 Battle.Mod.Manager 的分工：数据面管包在磁盘上的形态与两端一致性，本库管展示装配与用户管理，经 `IContentRegistryView` 校验展示键完整性，不依赖内容装配根 GameConfig。

## 职责

- mod 管理：`ModCatalog` 扫描 mods 根目录，产出 `ModPackage` 列表（启用态、是否含数据/展示代码、逐项错误、内容指纹），读写启用集，归集扫描期、数据装配期与展示装配期三段错误。
- 展示装配全过程 `ModAssets.Assemble`：建注册表 → 宿主内置先入表 → 逐 mod 装载展示代码 → 宿主把 mod 声明落地成资源对象 → 表就绪可查。内置注册与条目落地两步以委托交入，次序由本库执行，不由宿主记忆。
- 注册器实现 `DisplayRegistry`：同时实现写面与读面，资源以取供器登记、首次查询才解析并缓存；同键条目做字段级合并，mod 只声明图标时不会清空内置名称。
- 装配面 `ModDisplayRuntime`：一个 mod 一个写面实例，把该 mod 声明的展示键记进跨 mod 汇总的 `ModDeclaration`，错误按归属 mod 记录，并校验展示引用的内容键存在。
- 资源加载：`ModResourceLoader` 实现 `IModResourceLoader`，按 `ModAssetKey` 读 mod 包内图片与场景（带缓存、拒绝越出 mods 目录的寻址）。
- 资源获取入口：`ModAssets` 静态门面，`Skill/Buff/Dungeon/Unit/Texture/Scene` 六个查询 + `SetEnabled` 启停 + 装配期指纹。

## 边界外

- 不持有 `res://` 路径与可被 `.tres`/`.tscn` 引用的资源类：它们只留在 Godot 主工程，宿主以两个委托把内置注册与条目落地交进装配过程。
- 不做内容注册与门控：清单解析、依赖排序、BuffTypeId 段校验、指纹与启用集落盘在 Battle.Mod.Manager；内容定义注册在 GameConfig 的引导上下文。本库只经内容只读视图校验展示键。
- 不定义包布局：代码子目录名与清单文件名取自 `ModLayout`。
- 不做行为注册：mod 数据代码入口经 `IModEntry` 直接对接 GameConfig 的行为目录，本库不中转。

## 依赖

- Game.Mod.Interface（展示入口契约）、Game.Mod.Shared（注册器写面与读面、装配上下文、`ModAssetKey`）、Game.Shared（四个展示数据类型与 `DisplayAssetIds`）、Battle.Mod.Manager（`ModLayout`、`ModEntryLoader`、清单与启用集读写、`ModError`）、Battle.Shared（`IContentRegistryView` 与领域值类型）、GodotSharp（产出真实 Godot 对象）。
