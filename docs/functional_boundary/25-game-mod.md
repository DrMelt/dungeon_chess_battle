# DungeonChessBattle.Game.Mod

mod 包内 Godot 展示资源契约库。纯 .NET 类库，零依赖，定义 `godot_assets.json` 的 schema 与目录解析，只被 Game（Godot 装配）引用。展示契约与数据面 Battle.Mod 解耦：服务端程序集不携带表现层契约。

## 职责

- godot_assets.json 文件结构 DTO（技能 / Buff / 副本展示数据）与序列化上下文（源生成，零运行时反射）。
- 目录解析 `ModAssetsLoader`：从 mod 目录读取 godot_assets.json、定位 `images` 图标子目录，返回 `ModAssetsPackage`。

## 边界外

- 不做 Godot 装配：把展示数据构造成资源表条目是 Game 的 `ModAssetsMapper`（`functional_boundary/01`）。
- 不做数据面装载与合并：manifest / content 解析与键级合并在 Battle.Mod（`functional_boundary/24`）。
- 不依赖数据装载产物：解析只接受目录路径，与 `LoadedMod` 无耦合。

## 依赖

- 无。