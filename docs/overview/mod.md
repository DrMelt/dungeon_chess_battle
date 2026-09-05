# mod 域内部机制

覆盖 `DungeonChessBattle.Battle.Mod`（数据面）、`DungeonChessBattle.Game.Mod`（展示资源面）与 `GameConfig` 的内容装载面；客户端装配见 `functional_boundary/01` 与 `overview/godot`，服务端装配见 `functional_boundary/14`，内容一致性门控见 `flow/replay-design` 与房间校验一节。

## 装载管线

内置基座（`GameConfig.BuiltInContent`）与 user://mods 下的全部 mod 走同一管线：

```
ModLoader.LoadDirectory(user://mods)
  ├─ 逐目录解析 manifest.json + content.json
  ├─ 依赖拓扑排序：依赖者排后，同级按 Priority 升序再按 Id 字母序
  ├─ mod 代码装载：code/*.dll 经 AssemblyLoadContext 加载 → IModEntry.Initialize 注册行为
  └─ ContentMerge：按 Key 后写覆盖，BuffTypeId 跨键冲突抛异常
→ ContentSetRegistry（GameConfig）：DTO → 领域只读定义 + 索引 + DataRevision

ModAssetsLoader.Load(单个 mod 目录)
  └─ 解析 godot_assets.json + 定位 images 子目录 → ModAssetsPackage
→ ModAssetsMapper.Apply（Game）：构造成 Skill/Buff/Dungeon 展示资源注册进三张资源表
```

- 基座是最高优先级最低的一层，任何 mod 的 `priority` 都大于它，天然可被覆盖。
- 服务端 `Program` 读 `--mod-dir`；客户端 `ServerProcessHost` 以 `DCB_SERVER_MOD_DIR` 环境变量把同一 `user://mods` 传给子进程，两端内容指纹一致。
- Godot 展示资源（图标/名称/描述/特效场景引用）先经 `ModAssetsLoader` 从 mod 目录解析，再由 `ModAssetsMapper` 运行时构造注册进三张资源表，不经编辑器导入。展示数据解析失败只影响该 mod 的展示装配，数据面照常可用。

## 确定性约束

- `DataRevision` = 基座修订号 + 启用 mod 指纹（`ContentFingerprint`）。数据内容或代码 mod 任一变化都会改变它。
- 回放门控沿用双修订号：`DataRevision` 负责内容与布局侧，`BattleLogicRevision` 负责结算时序侧。
- 房间携带 `ContentFingerprint`，客户端进房比对本地 `DataRevision`，不一致拒绝加入——联机双方必须同源内容。
- BuffTypeId 段位：引擎段 1~999，mod 段 1000+，显式声明、冲突拒载，杜绝同步数值错位。

## 边界

- mod 不能携带自定义 Godot C# 脚本类（脚本注册构建期固化于主程序集）；表现脚本可用 PCK 内 GDScript，逻辑行为用 C# 代码 mod 的 `IModEntry`。
- 行为类别（技能效果/Buff 效果/AI/仇恨/阵营关系）以字符串 ID 注册进 `BehaviorCatalog`，内容数据引用 ID，两端行为目录同源。