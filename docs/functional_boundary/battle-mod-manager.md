# DungeonChessBattle.Battle.Mod.Manager

mod 包管理面：定义磁盘布局、扫描 mods 根目录、读写启用集、算内容指纹、以 ALC 装载代码入口。纯 .NET 类库，零项目引用，被 GameConfig（数据面装配）与 Game.Mod.Manager（展示面）引用。

## 职责

- 包布局 `ModLayout`：`manifest.json`、`code/*.dll`、`code_display/*.dll`、`mods.enabled.json` 四个名字与由目录推导路径的算法，是横跨数据面与展示面的唯一布局契约。
- manifest 与启用集的 System.Text.Json 源生成器序列化。
- 目录装载 `ModLoader`：解析清单、按启用集分流、依赖拓扑排序、代码摘要指纹计算；解析失败与被依赖拒载的目录进 `Unloaded` 单独列出。
- 启用集 `ModEnablement`：读写 mods 根目录内的停用列表，缺席即全部启用，两端读同一份文件即一致。
- 入口装载 `ModEntryLoader`：逐 mod 逐 DLL 以独立 ALC 找到入口接口实现并交回调，一个上下文只装一个程序集。
- 内容指纹 `ContentFingerprint`：以 manifest 字段与数据代码 DLL 摘要计算，展示代码与展示资源不入指纹。
- 错误归属 `ModError`：一条错误带归属 mod ID 与原因，消费方按 ID 分流。

## 边界外

- 不定义注册协议：入口契约 `IModEntry` 在 Battle.Mod.Interface，注册面在 Battle.Mod.Shared，行为 ID 常量在 Battle.Shared；本库只装载代码并调它的入口。
- 不把内容映射为领域对象：注册表 `ContentSetRegistry` 与内置基座注册在 GameConfig。
- 不实现行为本身：内置行为由 GameConfig 注册，mod 代码经 `IModEntry` 注册。
- 不感知展示面语义：只提供 `code_display` 目录名与通用装载机制，装载后注册进什么表由 Game.Mod.Manager 决定。
- 不编排装配流程：`ModLoader` 只产出 `ModLoadResult`，装配由 `GameConfig.ContentBootstrapper` 显式发起并消费它。
- 不做战斗与网络，不与 Godot 交互。

## 依赖

- 零项目引用：只用 BCL（`System.Text.Json` 源生成、`SHA256`、`AssemblyLoadContext`）。不依赖 `Battle.Mod.Interface`，mod 代码也看不到本库。
