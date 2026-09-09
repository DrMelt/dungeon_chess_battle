# DungeonChessBattle.Battle.Mod.Manager

mod 包管理面：解析包清单、扫描 mods 根目录、读写启用集、算内容指纹、以 ALC 装载代码入口。纯 .NET 类库，零项目引用，被 GameConfig（数据面装配）与 Game.Mod.Manager（展示面）引用。

## 职责

- 布局里不可配的那一半 `ModLayout`：`manifest.json` 与 `mods.enabled.json` 两个文件名，加 `code`/`code_display`/`assets` 三个默认目录名。产物落在 mod 目录何处由每个 mod 的清单声明（`code`/`codeLibraries`/`codeDisplay`/`codeDisplayLibraries`/`packages`），未声明才回落到默认目录。
- manifest 与启用集的 System.Text.Json 源生成器序列化；清单未知键即拒载——路径字段写错键名会静默回落，表现成「配了没生效」。
- 目录装载 `ModLoader`：解析清单、把声明的相对路径定位成绝对路径、按启用集分流、依赖拓扑排序、数据代码摘要指纹计算；解析失败与被依赖拒载的目录进 `Unloaded` 单独列出。
- 路径裁决：越界与语法非法整包拒载（两端读同一份清单，语法裁决必然一致）；数据面声明的产物缺失整包拒载；展示面产物缺失只留装载期错误——只有客户端判得到展示产物，让它参与拒载会让两端装载集合分叉。
- 相对路径安全闸 `ModRelativePath`：manifest 声明与 mod 资源寻址共用的「必须是根目录内的相对路径」判定。
- 启用集 `ModEnablement`：读写 mods 根目录内的停用列表，缺席即全部启用，两端读同一份文件即一致。
- 入口装载 `ModEntryLoader`：逐 mod 逐入口 DLL 以独立 ALC 找到入口接口实现并实例化，一个上下文只装一个程序集；装载顺序即清单声明顺序，不再枚举目录——枚举顺序由文件系统决定。装载与入口执行分离为两阶段：数据面一步完成，展示面先装载、居中挂载资源包、再执行入口。
- 内容指纹 `ContentFingerprint`：以 manifest 字段与数据入口及其探测目录内的 DLL 摘要计算，覆盖范围与数据面解析范围同源；展示代码与展示资源不入指纹。
- 错误归属 `ModError`：一条错误带归属 mod ID 与原因，消费方按 ID 分流。

## 边界外

- 不定义注册协议：入口契约 `IModEntry` 在 Battle.Mod.Interface，注册面在 Battle.Mod.Shared，行为 ID 常量在 Battle.Shared；本库只装载代码并调它的入口。
- 不把内容映射为领域对象：注册表 `ContentSetRegistry` 在 GameConfig，内容全部由 mod 经入口注册。
- 不实现行为本身：行为实现由内容 mod 经 `IModEntry` 注册进 `BehaviorCatalog`。
- 不感知展示面语义：只提供 `code_display` 默认目录名与通用装载机制，装载后注册进什么表由 Game.Mod.Manager 决定。
- 不编排装配流程：`ModLoader` 只产出 `ModLoadResult`，装配由 `GameConfig.ContentBootstrapper` 显式发起并消费它。
- 不做战斗与网络，不与 Godot 交互。

## 依赖

- 零项目引用：只用 BCL（`System.Text.Json` 源生成、`SHA256`、`AssemblyLoadContext`）。不依赖 `Battle.Mod.Interface`，mod 代码也看不到本库。
