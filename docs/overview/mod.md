# mod 域内部机制

覆盖 `DungeonChessBattle.Battle.Mod`（数据面契约）、`DungeonChessBattle.Game.Mod`（展示面与管理）、`DungeonChessBattle.Game.Shared`（展示契约）、`Battle.GameConfig`（行为/内容注册情境）与 `GameConfig` 的内容装配面。客户端装配见 `functional_boundary/01`，服务端装配见 `functional_boundary/14`，内容一致性门控见 `flow/replay-design` 与房间校验一节。

## 装配管线

内置基座（`GameConfig.BuiltInContent`）与 `user://mods` 下启用的 mod 走同一注册管线：内容全部以代码程序集的形式存在，宿主不解析任何内容 JSON。

```
ModCatalog.Scan(user://mods)                              Game.Mod：管理面，产出 ModPackage 列表与错误
  └─ ModLoader.LoadDirectory                              Battle.Mod
       ├─ 逐目录解析 manifest.json，附 code/*.dll 摘要
       ├─ 按 mods.enabled.json 分流启用与停用，被拒载的目录单独列出
       └─ 依赖拓扑排序：依赖者排后，同级按 Priority 升序再按 Id 字母序
ContentBootstrapper.Load(同一次扫描结果)                   GameConfig
  ├─ code/*.dll 经 AssemblyLoadContext 加载 → IModEntry.Initialize(IModBootstrapContext)
  │    行为经 IModRuntime 注册进行为目录，内容经 IModContentRuntime 注册进注册表
  └─ GameContentHost.CreateRegistry：内置基座先注册，mod 后注册同键覆盖；失败整体回退内置并把原因进 Errors
→ ContentSetRegistry：领域定义对象（SkillDefinition/BuffDefinition/UnitConfig/DungeonConfig）的注册表 + 索引 + DataRevision

var display = new DisplayRegistry()
BuiltinDisplayAssets.Register(display)                    Game：内置资源表条目与引擎预置场景名先入表
ModAssets.Initialize(catalog, registry, display)          Game.Mod
  └─ 逐 mod 装载 code_display/*.dll → IModDisplayEntry.Initialize 注册资源与视图
  └─ 展示键完整性校验：引用内容中不存在的技能/Buff/单位/副本只记错误，条目照常注册
→ ModAssetsMapper.Apply（Game）：被 mod 声明过的条目落地成 Mod*Resource 注册进三张资源表并回注索引
→ ModAssets.Publish(display)：UI 与表现层一律走 ModAssets 查
```

- 内容是代码而不是文件：数值/引用以领域对象经 `IModContentRuntime.RegisterXxx` 注册，技能引用 Buff 直接持有对象引用，编译期类型安全，不存在「引用未知字符串键」的运行时静默错位。覆改数值必须重编译数据 DLL，逃不过门控。
- 行为注册（`IModRuntime`）与内容注册（`IModContentRuntime`）合成 `IModBootstrapContext`——行为实现只依赖 `Battle.Shared` 契约（服务端可装载），展示资源引用 `Game.Shared`（仅客户端）。数据代码在 `code/`，展示代码在 `code_display/`，服务端只装 `code/`。
- 基座是最高优先级最低的一层，任何 mod 的 `priority` 都大于它，天然可被覆盖。
- 展示注册次序即覆盖次序：内置先入表、mod 后入表，同键条目由 mod 改写；合并是字段级的，mod 只声明图标时不会把内置名称一并清空。与数据面的行为目录同一套形状——注册什么由内容方定，宿主只递注册器。
- 服务端 `Program` 读 `--mod-dir`；客户端 `ServerProcessHost` 以 `DCB_SERVER_MOD_DIR` 把同一 `user://mods` 传给子进程。两端读同一目录即同一启用集、同一代码、同一指纹，不需要额外同步通道。
- 停用的 mod 若被启用中的 mod 依赖，依赖者报「依赖已停用」并整条不装载，不静默漏内容。
- 用户侧入口是主菜单的 mod 管理面板：列 mod（含解析失败与被拒载的目录）、切启用集、看逐项错误与数据修订号。面板只呈现 `ModCatalog` 的扫描结果，启停落盘后仍需重启进程才影响装配。
- 展示装配失败只影响展示面：单个 mod 的展示代码装载失败只跳过该 mod；引用了内容中不存在的键只记错误，条目照常注册，取不到的字段按未声明处理。
- 一个坏 mod 不 brick 游戏：数据入口装载或注册抛异常记一条错误并回退内置基座；回退不破确定性——两端读同一目录、跑同一份代码，失败同因同果，且 `DataRevision` 由 mod 列表而非注册结果算出。

## 确定性约束

- `DataRevision` = 基座修订号 +（有启用 mod 且含数据代码时）内容指纹。内容、mod 代码 DLL、启用集任一变化都会改变它。纯展示 mod（无 `code/`）不进指纹——展示字段不参与结算，两端展示不同不破坏确定性。无数据 mod 时指纹为空串，`DataRevision` 恒等于基座修订号——装配路径与懒初始化路径必须同值，否则无 mod 客户端进不了无 mod 房间。
- 回放门控沿用双修订号：`DataRevision` 负责内容与布局侧，`BattleLogicRevision` 负责结算时序侧。
- 房间携带 `ContentFingerprint`，客户端进房比对本地 `DataRevision`，不一致拒绝加入——联机双方必须同源内容。
- BuffTypeId 段位：引擎段 1~999，mod 段 1000+，显式声明、越段拒载。段校验只在 mod 注册时生效，内置注册走内部口不经校验，否则基座自己会被判越段。
- 展示代码与展示资源（图片与场景）不进指纹：展示字段不参与结算，两端展示不同不破坏确定性。别把它们加进门控。

## 边界

- mod 不能携带自定义 Godot C# 脚本类（脚本注册构建期固化于主程序集）；表现脚本可用 PCK 内 GDScript，逻辑行为用 C# 代码 mod 的 `IModEntry`。
- 可被 `.tres`/`.tscn` 引用的展示资源类与全部 `res://` 路径只能留在 Godot 主程序集内——资源文件按 `res://` 路径与 `script_class` 绑定脚本，而 `Game.Mod`/`Game.Shared` 都在 Godot 工程目录之外。因此展示装配分三步：宿主先向注册表登记内置（`BuiltinDisplayAssets.Register`），`Game.Mod` 再装载展示代码把声明注册进来，宿主把 mod 条目落地成资源对象后才 `Publish`。分步是这条约束的直接后果，不是设计洁癖。
- 资源名是全局命名空间：mod 在展示代码中以任意资源名经 `IModDisplayRuntime.RegisterTexture/RegisterScene` 注册包内图片与场景，任何条目都能按名引用任何包注册的资源，宿主内置的引擎预置场景也以同名机制登记。名字未注册即取不到对象，只报错不撤回条目。
- mod 资源寻址必须留在 mods 根目录内：`ModAssetKey` 声明的相对路径含 `..` 即拒绝解析，展示数据读不到 mods 目录外的文件。
- 行为类别（技能效果/Buff 效果/AI/仇恨/阵营关系）以字符串 ID 注册进 `BehaviorCatalog`，内容定义引用行为实例，两端行为目录同源。
- 启停只改启用集不改内容，且装配是一次性的：代码 mod 注册的委托强引用其 ALC 内类型，`Unload` 不回收，因此启停需重启进程生效，不支持热重载。
- mod 只能覆盖与新增，不能删除内置条目：内容注册与展示注册都是键级后写覆盖，无删除语义。
