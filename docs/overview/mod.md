# mod 域内部机制

覆盖数据面：`Battle.Mod.Interface`（入口契约）、`Battle.Mod.Shared`（注册面定义）、`Battle.Mod.Manager`（包管理）与 `Battle.GameConfig`（行为/内容注册情境）；
覆盖展示面：`Game.Mod.Manager`（展示装配与管理）、`Game.Mod.Interface`（mod 开发锚点）、`Game.Mod.Shared`（展示注册器定义）、`Game.Shared`（共用展示形状）与 `GameConfig` 的内容装配面。

客户端装配见 `functional_boundary/game`，服务端装配见 `functional_boundary/server-host`；内容一致性门控见 `flow/replay-design` 与房间校验一节。

引擎不内置任何游戏内容：单位/技能/Buff/副本全部由启用的数据 mod 提供。当前内容由基座 mod `DungeonChessBattle.BaseContent`（独立解决方案）以 `IModEntry` 注册，与其它 mod 走同一装配管线。

## 装配管线

启用的 mod 全部经 `user://mods`（服务端经 `--mod-dir`）装载；引擎无内置内容，注册表自空开始：

```
ModLoader.LoadDirectory(mods 根目录)                        Battle.Mod.Manager：目录扫描，产出 ModLoadResult
  ├─ 逐目录解析 manifest.json，按其声明定位入口/探测目录/资源包并算数据代码摘要
  ├─ 按 mods.enabled.json 分流启用与停用，被拒载的目录单独列出
  └─ 依赖拓扑排序：依赖者排后，同级按 Priority 升序再按 Id 字母序
ModCatalog.Scan（复用同一扫描）                            Game.Mod.Manager：管理面，产出 ModPackage 列表与三段错误
ContentBootstrapper.Load(扫描结果)                          GameConfig：只装配不扫描
  ├─ GameContentHost.CreateRegistry：创建空注册表与空行为目录
  └─ ModEntryLoader 逐 mod 装载声明的数据入口 DLL → IModEntry.Initialize(IModBootstrapContext)
       行为经 IModRuntime 注册进行为目录，内容经 IModContentRuntime 注册进注册表
→ ContentSetRegistry：领域定义对象（SkillDefinition/BuffDefinition/UnitConfig/DungeonConfig）的注册表 + 索引 + DataRevision，以只读视图 IContentRegistryView 交给展示面

ModAssets.Assemble(catalog, content, registerBuiltin, mountPacks, applyResources, modsRoot)   Game.Mod.Manager
  ├─ new DisplayRegistry → registerBuiltin(display)       Game：引擎预置场景名先入表（图标与资源表条目已随 mod 迁出，此处只报场景）
  ├─ ModEntryLoader 逐 mod 装载声明的展示入口 DLL       Battle.Mod.Manager：只装载找入口实现，不执行入口
  ├─ mountPacks(enabled mods)                             Game：逐 mod 挂载 manifest 声明的资源包：ProjectSettings.LoadResourcePack，包内资源以 res://mods/{id}/ 前缀寻址
  ├─ 逐 mod 执行 IModDisplayEntry.Initialize              装载包内展示 .tres 与资源并注册展示数据
  │    展示键完整性校验：引用内容中不存在的技能/Buff/单位/副本按归属 mod 记错误，条目照常注册
  └─ applyResources(declared, display)                    Game：被 mod 声明的条目落地成 Mod*Resource 注册进三张资源表并回注索引
→ 注册表就绪：UI 与表现层一律走 ModAssets 查
```

- 内容是代码而不是文件：数值/引用以领域对象经 `IModContentRuntime.RegisterXxx` 注册，技能引用 Buff 直接持有对象引用，编译期类型安全，不存在「引用未知字符串键」的运行时静默错位。
- 约束：覆改数值必须重编译数据 DLL，逃不过门控。
- 行为注册（`IModRuntime`）与内容注册（`IModContentRuntime`）合成 `IModBootstrapContext`——行为实现只依赖 `Battle.Shared` 契约（服务端可装载）；展示注册器定义在 `Game.Mod.Shared`，收发的展示数据在 `Game.Shared`（仅客户端）。
- 代码与资源包的位置由 manifest 声明：`code` 是数据入口 DLL 列表（顺序即装载顺序），`codeDisplay` 是展示入口 DLL 列表，`packages` 是待挂载资源包列表，`codeLibraries`/`codeDisplayLibraries` 是额外的依赖探测目录。未声明的字段回落到默认目录 `code/`、`code_display/`、`assets/` 下的同名产物枚举；入口 DLL 所在目录始终自动登记为该面的探测根。服务端只装数据入口。
- 声明即承诺存在：`code` 里的文件缺失整包拒载，`codeDisplay`/`packages` 缺失只记装载期错误。路径必须落在 mod 目录内，越界即拒载。
- 内容全部来自 mod：引擎无内置内容，行为目录与注册表自空开始，任何 mod 的 `priority` 都决定其覆盖次序，越靠后装载的同键内容覆盖越靠前者。
- 展示注册次序即覆盖次序：引擎预置场景先入表（`BuiltinDisplayAssets.Register`）、mod 展示数据后入表，同键条目由 mod 改写；合并是字段级的，mod 只声明图标时不会把已有名称一并清空。与数据面的行为目录同一套形状——注册什么由内容方定，宿主只递注册器。单位侧宿主先为每个内容单位登记空占位数据（显示名回退配置键），mod 的单位条目同样走 `.tres`：`ModelScene` 由 `.tres` 直引包内 `.tscn`，未配置即回落共享模板 `unit_game_show.tscn`，外观取场景自带材质（`BodyColor` 只是代码侧的覆写通道）。模型与配色是纯客户端展示数据，不进指纹。
- 服务端 `Program` 读 `--mod-dir`，装配前先 `ModLoader.LoadDirectory` 再 `ContentBootstrapper.Load`；客户端 `ServerProcessHost` 以 `DCB_SERVER_MOD_DIR` 把同一 `user://mods` 传给子进程，Godot 端由 `ModManager` 先扫后装。
- 两端读同一目录即同一启用集、同一代码、同一指纹，不需要额外同步通道。
- 停用的 mod 若被启用中的 mod 依赖，依赖者报「依赖已停用」并整条不装载，不静默漏内容。
- 用户侧入口是主菜单的 mod 管理面板：列 mod（含解析失败与被拒载的目录）、切启用集、看逐项错误与数据修订号。面板只呈现 `ModCatalog` 的扫描结果，启停落盘后仍需重启进程才影响装配。
- 展示装配失败只影响展示面：单个 mod 的展示代码装载失败只跳过该 mod；引用了内容中不存在的键只记错误，条目照常注册，取不到的字段按未声明处理。
- 一个坏 mod 不 brick 游戏：数据入口装载或注册抛异常记一条错误并跳过该 mod；跳过不破确定性——两端读同一目录、跑同一份代码，失败同因同果，且 `DataRevision` 由 mod 列表而非注册结果算出。全部 mod 装载失败时注册表为空，作无内容处理。

## 确定性约束

- `DataRevision` = 引擎内容修订号 +（有启用 mod 且含数据代码时）内容指纹；内容、mod 代码 DLL、启用集任一变化都会改变它。引擎内容修订号为固定常量 `GameContentHost.EngineRevision`，内容修订全部由 mod 指纹承担。
- 纯展示 mod（无数据入口）不进指纹——展示字段不参与结算，两端展示不同不破坏确定性。
- 解析范围与指纹范围同源：`CodeHash` 覆盖 `code` 声明的入口与其探测目录（含各入口自身所在目录）顶层的全部 DLL。声明了新目录却没让它进指纹，等于给门控留缺口——换了未被哈希的 DLL 而两端都认为自己兼容。
- 拒载裁决只依据两端都能判的事实：清单语法、路径越界、数据面产物缺失。展示产物只有客户端目录里有，它缺失只记错误、不改变装载集合，否则两端 `DataRevision` 分叉。
- 约束：无数据 mod 时指纹为空串，`DataRevision` 恒等于引擎内容修订号——装配路径与懒初始化路径必须同值，否则无 mod 客户端进不了无 mod 房间。
- 回放门控沿用双修订号：`DataRevision` 负责内容与布局侧，`BattleLogicRevision` 负责结算时序侧。
- 房间携带 `ContentFingerprint`，客户端进房比对本地 `DataRevision`，不一致拒绝加入——联机双方必须同源内容。
- BuffTypeId 段位契约：1~999 为引擎保留段（引擎不注册任何 Buff，段位留给后续引擎内建），mod 必须声明 1000 及以上，越段拒载。它是跨端同步身份，改段位等于改同步语义。
- 约束：段校验只在 mod 注册时生效；引擎不注册任何 Buff，不存在绕过段校验的内部注册路径。
- 展示代码与展示资源（图片与场景）不进指纹：展示字段不参与结算，两端展示不同不破坏确定性。别把它们加进门控。

## 边界

- mod 展示代码工程是带资源的 Godot C# 工程：展示 DLL 由 manifest 的 `codeDisplay` 声明（默认 `code_display/`）、宿主经 ALC 装载 C# 类型（入口契约与 `.tres` 脚本类），展示数据 `.tres` 与图标随 `packages` 声明的资源包分发（默认 `assets/*.pck`）。`.tres` 按 `res://` 路径绑定脚本：脚本类 `.cs` 与 `.tres` 同在展示工程 `mods/{id}/` 目录下随包导出，类型实体由展示入口 DLL 提供，路径与类型缺一即装载返回 null、该条目被丢弃。
- 可被 `.tres`/`.tscn` 引用的资源类与 `res://` 路径原则上随资源文件归属：mod 展示 `.tres`、资源脚本与图标经 `assets/*.pck` 打包（`ProjectSettings.LoadResourcePack` 挂载后以 `res://mods/{id}/` 前缀访问），引擎预置场景仍留在 Godot 主工程、经 `DisplayAssetIds` 登记。跨工程的 `.tres`/`.tscn` 脚本引用沿既有约束：资源文件按 `res://` 路径绑定脚本，脚本须在加载侧可解析——mod 侧是随包导出的 mod 展示工程 C# 脚本类，宿主侧是主工程内 C# 脚本类。
- 资源包在 mod 目录里的位置可配，包内的虚拟路径不可配：`.pck` 放在 mod 目录何处都行，但包内资源必须以 `res://mods/{id}/` 前缀落位——挂载是 `ProjectSettings.LoadResourcePack`，寻址按包内 `res://` 路径，两者与 pck 文件的磁盘位置无关。
- 因此展示装配的四步由 `Game.Mod.Manager` 用三个委托把宿主步骤串起来：宿主登记引擎预置场景名与单位外观占位（`BuiltinDisplayAssets.Register`）→ Manager 装载展示代码 → 宿主挂载展示资源包（`MountAssetPacks`）→ Manager 执行入口把声明注册进来 → 宿主把 mod 条目落地成资源对象（`ModAssetsMapper.Apply`）。次序固定在 `ModAssets.Assemble` 内部，宿主只提供三步实现，不记忆流程。装载先于挂载、入口执行后于挂载，是「入口要经包内资源读数据、契约程序集须宿主已装载」两重顺序约束的直接后果，不是设计洁癖。
- mod 在展示代码中能读到注册表：`ModDisplayContext.Registry` 是实时只读视图，读到什么取决于注册到此为止的次序——引擎预置场景名与先装载的 mod 已入表。mod 据此改写已声明条目而不必先验其内容，跨包引用宿主对象按 `DisplayAssetIds` 的名查，不要复制一份资源。
- mod 的契约与形状程序集只在宿主已加载时可解析：`ModAssemblyLoader` 先按 `AssemblyName.FullName` 比对进程内已加载的程序集（GodotSharp 与契约程序集可能不在 Default 上下文），命中即共用主副本，未命中才回退 mod 目录探测。`ModAssets.Assemble` 里引擎侧注册早于展示代码装载，这条次序保证 `Game.Mod.Shared` 与 `Game.Shared` 在装载展示 DLL 前已加载——把其中的常量类改成「用到才加载」的形态即破坏该前提。
- 资源名是全局命名空间：mod 在展示代码中以任意资源名经 `IModDisplayRuntime.RegisterTexture/RegisterScene` 注册包内图片与场景，任何条目都能按名引用任何包注册的资源，宿主登记的引擎预置场景也以同名机制入表。名字未注册即取不到对象，只报错不撤回条目。
- mod 侧提供的一切相对路径都过同一个闸 `ModRelativePath`：`ModAssetKey` 的寻址与 manifest 声明的产物路径同样必须落在自己的根目录内，含 `..`、绝对路径、盘符或 UNC 即拒绝，展示数据读不到 mods 目录外的文件、装载器也载不到 mod 目录外的 DLL。
- 行为类别（技能效果/Buff 效果/AI/仇恨/阵营关系）以字符串 ID 注册进 `BehaviorCatalog`，内容定义引用行为实例，两端行为目录同源。
- 启停只改启用集不改内容，且装配是一次性的：代码 mod 注册的委托强引用其 ALC 内类型，`Unload` 不回收，因此启停需重启进程生效，不支持热重载。
- mod 只能覆盖与新增，不能删除条目：内容注册与展示注册都是键级后写覆盖，无删除语义。
