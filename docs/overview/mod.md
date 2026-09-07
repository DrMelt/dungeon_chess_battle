# mod 域内部机制

覆盖数据面：`Battle.Mod.Interface`（入口契约）、`Battle.Mod.Shared`（注册面定义）、`Battle.Mod.Manager`（包管理）与 `Battle.GameConfig`（行为/内容注册情境）；
覆盖展示面：`Game.Mod.Manager`（展示装配与管理）、`Game.Mod.Interface`（mod 开发锚点）、`Game.Mod.Shared`（展示注册器定义）、`Game.Shared`（共用展示形状）与 `GameConfig` 的内容装配面。

客户端装配见 `functional_boundary/game`，服务端装配见 `functional_boundary/server-host`；内容一致性门控见 `flow/replay-design` 与房间校验一节。

## 装配管线

内置基座（`GameConfig.BuiltInContent`）与 `user://mods` 下启用的 mod 走同一注册管线：内容全部以代码程序集的形式存在，宿主不解析任何内容 JSON。

```
ModLoader.LoadDirectory(mods 根目录)                        Battle.Mod.Manager：目录扫描，产出 ModLoadResult
  ├─ 逐目录解析 manifest.json，附 code/*.dll 摘要
  ├─ 按 mods.enabled.json 分流启用与停用，被拒载的目录单独列出
  └─ 依赖拓扑排序：依赖者排后，同级按 Priority 升序再按 Id 字母序
ModCatalog.Scan（复用同一扫描）                            Game.Mod.Manager：管理面，产出 ModPackage 列表与三段错误
ContentBootstrapper.Load(扫描结果)                          GameConfig：只装配不扫描，内置先注册 mod 后覆盖
  ├─ ModEntryLoader 逐 mod 装载 code/*.dll → IModEntry.Initialize(IModBootstrapContext)
  │    行为经 IModRuntime 注册进行为目录，内容经 IModContentRuntime 注册进注册表
  └─ GameContentHost.CreateRegistry：内置基座先注册，mod 后注册同键覆盖；失败整体回退内置并把原因进 Errors
→ ContentSetRegistry：领域定义对象（SkillDefinition/BuffDefinition/UnitConfig/DungeonConfig）的注册表 + 索引 + DataRevision，以只读视图 IContentRegistryView 交给展示面

ModAssets.Assemble(catalog, content, registerBuiltin, applyResources, modsRoot)   Game.Mod.Manager
  ├─ new DisplayRegistry → registerBuiltin(display)       Game：内置资源表条目与引擎预置场景名先入表
  ├─ ModEntryLoader 逐 mod 装载 code_display/*.dll → IModDisplayEntry.Initialize 注册资源与展示数据
  │    展示键完整性校验：引用内容中不存在的技能/Buff/单位/副本按归属 mod 记错误，条目照常注册
  └─ applyResources(declared, display)                    Game：被 mod 声明的条目落地成 Mod*Resource 注册进三张资源表并回注索引
→ 注册表就绪：UI 与表现层一律走 ModAssets 查
```

- 内容是代码而不是文件：数值/引用以领域对象经 `IModContentRuntime.RegisterXxx` 注册，技能引用 Buff 直接持有对象引用，编译期类型安全，不存在「引用未知字符串键」的运行时静默错位。
- 约束：覆改数值必须重编译数据 DLL，逃不过门控。
- 行为注册（`IModRuntime`）与内容注册（`IModContentRuntime`）合成 `IModBootstrapContext`——行为实现只依赖 `Battle.Shared` 契约（服务端可装载）；展示注册器定义在 `Game.Mod.Shared`，收发的展示数据在 `Game.Shared`（仅客户端）。
- 数据代码在 `code/`，展示代码在 `code_display/`，服务端只装 `code/`。
- 基座是最高优先级最低的一层，任何 mod 的 `priority` 都大于它，天然可被覆盖。
- 展示注册次序即覆盖次序：内置先入表、mod 后入表，同键条目由 mod 改写；合并是字段级的，mod 只声明图标时不会把内置名称一并清空。与数据面的行为目录同一套形状——注册什么由内容方定，宿主只递注册器。单位没有编辑器资源表：`BuiltinDisplayAssets` 为每个内容单位注册空占位数据（显示名回退配置键），mod 经 `UnitDisplay.ModelScene`/`BodyColor` 覆盖模型与配色，未覆盖即回落共享模板 `unit_game_show.tscn`；模型与配色是纯客户端展示数据，不进指纹。
- 服务端 `Program` 读 `--mod-dir`，装配前先 `ModLoader.LoadDirectory` 再 `ContentBootstrapper.Load`；客户端 `ServerProcessHost` 以 `DCB_SERVER_MOD_DIR` 把同一 `user://mods` 传给子进程，Godot 端由 `ModManager` 先扫后装。
- 两端读同一目录即同一启用集、同一代码、同一指纹，不需要额外同步通道。
- 停用的 mod 若被启用中的 mod 依赖，依赖者报「依赖已停用」并整条不装载，不静默漏内容。
- 用户侧入口是主菜单的 mod 管理面板：列 mod（含解析失败与被拒载的目录）、切启用集、看逐项错误与数据修订号。面板只呈现 `ModCatalog` 的扫描结果，启停落盘后仍需重启进程才影响装配。
- 展示装配失败只影响展示面：单个 mod 的展示代码装载失败只跳过该 mod；引用了内容中不存在的键只记错误，条目照常注册，取不到的字段按未声明处理。
- 一个坏 mod 不 brick 游戏：数据入口装载或注册抛异常记一条错误并回退内置基座；回退不破确定性——两端读同一目录、跑同一份代码，失败同因同果，且 `DataRevision` 由 mod 列表而非注册结果算出。

## 确定性约束

- `DataRevision` = 基座修订号 +（有启用 mod 且含数据代码时）内容指纹；内容、mod 代码 DLL、启用集任一变化都会改变它。
- 纯展示 mod（无 `code/`）不进指纹——展示字段不参与结算，两端展示不同不破坏确定性。
- 约束：无数据 mod 时指纹为空串，`DataRevision` 恒等于基座修订号——装配路径与懒初始化路径必须同值，否则无 mod 客户端进不了无 mod 房间。
- 回放门控沿用双修订号：`DataRevision` 负责内容与布局侧，`BattleLogicRevision` 负责结算时序侧。
- 房间携带 `ContentFingerprint`，客户端进房比对本地 `DataRevision`，不一致拒绝加入——联机双方必须同源内容。
- BuffTypeId 段位契约（引擎段 1~999、mod 段 1000+）在 `mod-development.md` 对外声明。
- 约束：段校验只在 mod 注册时生效，内置注册走内部口不经校验，否则基座自己会被判越段。
- 展示代码与展示资源（图片与场景）不进指纹：展示字段不参与结算，两端展示不同不破坏确定性。别把它们加进门控。

## 边界

- mod 不能携带自定义 Godot C# 脚本类（脚本注册构建期固化于主程序集）；表现脚本可用 PCK 内 GDScript，逻辑行为用 C# 代码 mod 的 `IModEntry`。
- 可被 `.tres`/`.tscn` 引用的展示资源类与全部 `res://` 路径只能留在 Godot 主程序集内——资源文件按 `res://` 路径与 `script_class` 绑定脚本，而 `Game.Mod.Manager`/`Game.Mod.Interface`/`Game.Mod.Shared`/`Game.Shared` 都在 Godot 工程目录之外。
- 因此展示装配的三步由 `Game.Mod.Manager` 用两个委托把宿主步骤串起来：宿主登记内置（`BuiltinDisplayAssets.Register`）→ Manager 装载展示代码把声明注册进来 → 宿主把 mod 条目落地成资源对象（`ModAssetsMapper.Apply`）。三步次序固定在 `ModAssets.Assemble` 内部，宿主只提供两步实现，不记忆流程。分步是那条约束的直接后果，不是设计洁癖。
- mod 在展示代码中能读到注册表：`ModDisplayContext.Registry` 是实时只读视图，读到什么取决于注册到此为止的次序——内置与先装载的 mod 已入表。mod 据此改写已声明条目而不必先验其内容，跨包引用宿主对象按 `DisplayAssetIds` 的名查，不要复制一份资源。
- mod 的契约与形状程序集只在宿主已加载时可解析：`ModAssemblyLoader` 先按 `AssemblyName.FullName` 比对 Default 上下文，命中即共用主副本，未命中才回退 mod 目录探测。`ModAssets.Assemble` 里内置注册早于展示代码装载，这条次序保证 `Game.Mod.Shared` 与 `Game.Shared` 在装载展示 DLL 前已加载——把其中的常量类改成「用到才加载」的形态即破坏该前提。
- 资源名是全局命名空间：mod 在展示代码中以任意资源名经 `IModDisplayRuntime.RegisterTexture/RegisterScene` 注册包内图片与场景，任何条目都能按名引用任何包注册的资源，宿主内置的引擎预置场景也以同名机制登记。名字未注册即取不到对象，只报错不撤回条目。
- mod 资源寻址必须留在 mods 根目录内：`ModAssetKey` 声明的相对路径含 `..` 即拒绝解析，展示数据读不到 mods 目录外的文件。
- 行为类别（技能效果/Buff 效果/AI/仇恨/阵营关系）以字符串 ID 注册进 `BehaviorCatalog`，内容定义引用行为实例，两端行为目录同源。
- 启停只改启用集不改内容，且装配是一次性的：代码 mod 注册的委托强引用其 ALC 内类型，`Unload` 不回收，因此启停需重启进程生效，不支持热重载。
- mod 只能覆盖与新增，不能删除内置条目：内容注册与展示注册都是键级后写覆盖，无删除语义。
