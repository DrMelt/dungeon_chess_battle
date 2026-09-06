# DungeonChessBattle.Game.Mod

Godot 端 mod 子系统：管 mod 包、装配 mod 展示代码、提供资源加载与统一获取入口。只被 `Game` 引用，服务端程序集不携带。

与数据面 `Battle.Mod`（`functional_boundary/24`）的分工：数据面管「内容是什么、两端是否一致」，本库管「客户端怎么把它显示出来、用户怎么管这些包」。本库经 `GameConfig` 读内容注册表，仅为展示键完整性校验，不承载数据面逻辑。

## 职责

- mod 管理：`ModCatalog` 扫描 mods 根目录，产出 `ModPackage` 列表（启用态、是否含数据/展示代码、逐项错误、内容指纹），并读写启用集；解析失败与被依赖拒载的目录同样列出并带原因。
- 展示装配：`ModAssets.Initialize` 逐 mod 装载 `code_display/*.dll`，找到 `IModDisplayEntry` 后调接口把资源与视图注册进宿主给出的 `DisplayRegistry`；`ModDisplayRuntime` 包装注册表、记录 mod 声明过的键并校验引用内容存在的键。
- 展示注册表 `DisplayRegistry`：实现 `IModDisplayRuntime`（注册面）与 `IDisplayRegistry`（查询面），同键条目字段级合并（mod 只声明图标不清空内置名称）。
- 资源加载：`ModResourceLoader` 实现 `Game.Shared` 的 `IModResourceLoader`，按 `ModAssetKey` 读 mod 包内图片与场景（带缓存、拒绝越出 mods 目录的寻址）。
- 资源获取入口：`ModAssets` 静态门面，`Skill/Buff/Dungeon/Unit/Texture/Scene` 六个查询 + `SetEnabled` 启停 + 装配期指纹。

## 边界外

- 不把视图落成 Godot 资源对象：可被 `.tres` 引用的资源类只能留在 Godot 主程序集，`Mod*Resource` 与三张资源表在 `Game`（`functional_boundary/01`），本库只交出视图数据。
- 不做内容注册与门控：manifest 解析、依赖排序、BuffTypeId 段校验、指纹与启用集落盘在 `Battle.Mod`；内容定义注册在 `GameConfig` 的引导上下文。本库只把内容注册表当只读校验源。
- 不登记任何 `res://` 路径：引擎预置场景由宿主以资源名注册进同一张注册表，本库不认识工程内路径。
- 不做行为注册：mod 代码入口经 `IModEntry` 直接对接 `GameConfig` 的行为目录，本库不中转。

## 依赖

- `Game.Shared`（视图、注册面与加载契约）、`Battle.Mod`（清单、`LoadedMod`、启用集读写）、`Battle.GameConfig`（内容注册表只读校验）、GodotSharp（产出真实 Godot 对象）。
- 展示资源名与行为 ID 是同一套机制：注册方自己决定登记什么，宿主只递注册器；覆盖靠注册次序（内置在前、mod 在后）与字段级合并，不靠特判来源。
