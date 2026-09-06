# DungeonChessBattle.Battle.Mod

mod 内容装载契约库（数据面）。纯 .NET 类库，零 Godot 依赖，被 GameConfig（行为/内容注册情境）与 Game（Godot 装配）引用。服务于「所有配置与资源都能以 mod 方式加载」：mod 以代码程序集形式存在，经接口注册，宿主管装载与顺序。展示资源不进本库，见 `functional_boundary/25`。

## 职责

- 定义 mod 包数据面结构契约：`manifest.json`、`code/*.dll`、`code_display/*.dll`（展示目录由 Game.Mod 使用）、`mods.enabled.json`。
- manifest 与启用集的 System.Text.Json 源生成器序列化（零运行时反射）。
- 目录装载 `ModLoader`：解析清单、按启用集分流、依赖拓扑排序、代码摘要指纹计算；解析失败与被依赖拒载的目录进 `Unloaded` 单独列出，供管理面一个不漏地展示。
- 启用集 `ModEnablement`：读写 mods 根目录内的停用列表，缺席即全部启用。放在根目录内是为了让服务端与客户端读同一份文件即两端一致，不必扩传参通道。
- mod 代码装载契约 `IModEntry` / `IModBootstrapContext`（行为注册面 + 内容注册面），与 `ModAssemblyLoader` 的泛型 ALC 实现。
- 内容指纹 `ContentFingerprint`：以 manifest 字段与数据代码 DLL 摘要计算，展示代码与展示资源不入指纹。
- 行为 ID 常量 `BehaviorIds`：content 行为字段引用内置行为目录时使用。

## 边界外

- 不把内容映射为领域对象：注册表 `ContentSetRegistry` 与内置基座注册在 GameConfig。
- 不实现行为本身（`ISkillEffect` 等只有 Shared 端口）：内置行为由 GameConfig 注册，mod 代码经 `IModEntry` 注册。
- 不解析展示资源与展示代码：`code_display/*.dll` 的 schema 与装载在 `DungeonChessBattle.Game.Mod`，本库对其只知目录名约定、不做装载。
- 不做战斗与网络，不与 Godot 交互。

## 依赖

- Battle.Shared（行为端口与内容定义类型）。不依赖 Game.Mod（表现面是独立零依赖库）。