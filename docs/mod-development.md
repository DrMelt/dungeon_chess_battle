# mod 开发向导

面向 mod 开发者的外部指南。mod 契约经私有 NuGet 本地源分发，编译期只暴露入口契约与注册面，宿主实现（各 Manager、GameConfig）不进包。

## 包与版本

| 包（引用锚点） | 传递引用闭包 | 适用 |
| --- | --- | --- |
| `DungeonChessBattle.Battle.Mod.Interface` | `Battle.Mod.Shared`、`Battle.Shared` | 数据代码 `code/` |
| `DungeonChessBattle.Game.Mod.Interface` | `Game.Mod.Shared`、`Game.Shared` | 展示代码 `code_display/` |

- 全部契约包版本号等于游戏发布版本，mod 必须用与游戏同版本的契约包编译——装载器按程序集全名匹配宿主副本，版本不一致即静默不装载。
- 契约包经 `tools/pack-mod-sdk.ps1` 打包到 `artifacts/nuget` 本地源目录，mod 开发者把它登记为自己的 NuGet 源后还原；数据面包是纯 .NET 类库；展示面包带 `GodotSharp` 依赖项，该包发布在 nuget.org 上（Godot 官方发布），mod 展示工程经 `Godot.NET.Sdk` 还原，版本与主工程对齐。

## 工程要求

- 数据代码工程：`Microsoft.NET.Sdk` 类库，`net10.0`，只引 `Battle.Mod.Interface`。
- 展示代码工程：`Godot.NET.Sdk/4.7.1`（与主工程同版本），只引 `Game.Mod.Interface`，需本机安装 Godot 4.7。
- 数据与展示兼有的 mod 拆两个工程、产两个 DLL：数据 DLL 进 `code/`（服务端装载，禁止引用 Godot），展示 DLL 进 `code_display/`（仅客户端）。

## 包布局与 manifest

mods 根目录下每 mod 一个目录，目录名即 `Id`：

```
mods/
└── my_mod/
    ├── manifest.json
    ├── code/*.dll
    └── code_display/*.dll
```

`manifest.json`（camelCase）：

```json
{
  "id": "my_mod",
  "name": "My Mod",
  "version": "1.0.0",
  "revision": "0",
  "dependencies": [],
  "priority": 10
}
```

- `revision`：数据代码变更时递增，参与内容指纹，改动必改。
- `priority`：覆盖次序，数值大者后装载；内置基座低于任何 mod，天然可被覆盖。
- `dependencies`：依赖的 mod ID，拓扑排序后先装载；被删或停用的依赖致依赖者整条拒载。

启停集为 mods 根目录内 `mods.enabled.json`，缺席即全部启用。DataRevision 与房间进出门控见 `overview/mod`。

## 数据代码

实现 `IModEntry`，在 `Initialize(IModBootstrapContext)` 里注册行为与内容：

- 行为经 `IModRuntime` 按字符串 ID 注册（技能效果、Buff 效果、敌人决策、仇恨规则、阵营关系），并可在构造定义时按 ID 取回实例。
- 内容经 `IModContentRuntime` 直接注册强类型定义对象（`SkillDefinition`/`BuffDefinition`/`UnitConfig`/`DungeonConfig`），不写 JSON schema。
- 覆盖规则：同键后写覆盖，只增不改删。BuffTypeId 段位 1~999 归引擎，mod 段 1000+，越段拒载。
- 行为实现必须无状态，可被任意单位与房间共享；行为 ID 常量和内容形状见 `Battle.Shared`。

## 展示代码

实现 `IModDisplayEntry`，在 `Initialize(IModDisplayRuntime, ModDisplayContext)` 里注册展示资源与展示数据：

- 纹理与场景按资源名注册（全局命名空间，跨 mod 可互引），展示数据按身份键注册并做字段级合并——mod 只声明图标不会清空内置名称。
- 经 `ModDisplayContext.Registry` 读当前注册表，据此改写已声明条目；已注册数据与资源名见 `Game.Shared.Display`。
- 包内资源寻址用 `ModAssetKey`（mod ID + 相对路径），相对路径含 `..` 即拒载。

## 发布

按布局把 `manifest.json` 与两个 DLL 放进 mods 根目录（游戏内为 `user://mods`），在游戏主菜单 mod 面板启停，重启进程后生效，不支持热重载。

契约包按 GPL-3.0 分发，mod 与游戏同显式许可，注意授权一致性。
