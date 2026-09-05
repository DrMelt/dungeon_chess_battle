# DungeonChessBattle.Battle.Mod

mod 内容装载契约库（数据面）。纯 .NET 类库，零 Godot 依赖，被 GameConfig（语义映射）与 Game（Godot 装配）引用。服务于「所有配置与资源都能以 mod 方式加载」：内置内容与用户 mod 走同一条装载-合并-映射管线。展示资源 `godot_assets.json` 不在本库，见 `functional_boundary/25`。

## 职责

- 定义 mod 包数据面结构契约：`manifest.json`、`content.json`、`code/*.dll`；展示资源 `godot_assets.json` 由 Game.Mod 定义。
- 内容 schema DTO 与 System.Text.Json 源生成器序列化（零运行时反射）。
- 目录装载 `ModLoader`：解析清单与内容、依赖拓扑排序、优先级合并、BuffTypeId 段冲突校验、内容指纹计算。
- 代码 mod 装载契约 `IModEntry` / `IModRuntime`（行为注册面），与 `ModAssemblyLoader` 的 ALC 实现。
- 行为 ID 常量 `BehaviorIds`：content.json 行为字段引用内置行为目录时使用。

## 边界外

- 不把内容映射为领域对象（`SkillDefinition` 等）：那是 GameConfig 的 `ContentSetRegistry`。
- 不实现行为本身（`ISkillEffect` 等只有 Shared 端口）：内置行为由 GameConfig 注册，mod 代码经 `IModEntry` 注册。
- 不解析展示资源：godot_assets.json 的 schema 与目录解析在 `DungeonChessBattle.Game.Mod`，本库对其一无所知。
- 不做战斗与网络，不与 Godot 交互。

## 依赖

- Battle.Shared（行为端口类型）。不依赖 Game.Mod（表现面是独立零依赖库）。