using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonChessBattle.Battle.Config.Registry;
using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Game.Display.Registry;
using DungeonChessBattle.Game.Mod.Manager;
using Godot;
using Godot.Bridge;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// Godot 端 mod 装配编排：扫描启用集 → 挂载各 mod 资源包 → 数据面装配 → 展示面装配 → 展示覆盖巡检。
/// 主场景 _Ready 首个调用，保证任何 UI 取数前内容已就绪；
/// 服务器子进程由 ServerProcessHost 注入同一 mods 目录绝对路径，两端读同一启用集与内容即同源。
/// 两次装配的产物分别写入 <see cref="ServiceLocator.ContentRegistry"/> 与 <see cref="ServiceLocator.ModAssets"/>，
/// 展示装配内部的先后次序不在这里，见 <see cref="ModAssets.Assemble"/>。
/// </summary>
public static class ModManager {
    /// <summary>mods 目录名，位于游戏可执行文件所在目录下。</summary>
    private const string ModsDirName = "mods";

    /// <summary>mods 根目录绝对路径：游戏可执行文件所在目录下的 mods。</summary>
    public static string ModsRootPath => OS.GetExecutablePath().GetBaseDir().PathJoin(ModsDirName);

    private static readonly ILogger Logger = ServiceLocator.CreateLogger(nameof(ModManager));

    private static bool _initialized;

    /// <summary>
    /// 执行一次装配，幂等；单个 mod 失败不中止其余 mod，错误汇总进装配产物的 <c>ModCatalog</c>。
    /// 扫描、装配与展示装配的过程日志由下层按自身类别名记录，本类只注入通道并补记展示覆盖巡检结果。
    /// 幂等标志在装配成功后置位：装配中断可以重试，不留下「标志已置位、内容永久未就绪」的中间态。
    /// </summary>
    public static void EnsureInitialized() {
        if (_initialized)
            return;

        try {
            var catalog = ModCatalog.Scan(ModsRootPath, ServiceLocator.LoggerFactory);
            // 复用同一次扫描结果装配数据面：启停文件已随扫描读入，两端不必再传参。
            // Load(扫描结果) 只带回装配期新增错误，扫描期错误已在 catalog.Errors
            var boot = ContentBootstrapper.Load(catalog.ScanResult, ServiceLocator.LoggerFactory);
            catalog.RecordAssemblyErrors(boot.Errors);
            // 数据面产物交组合根持有，UI 经 ServiceLocator.ContentRegistry 取内容定义
            ServiceLocator.BindContent(boot.Registry);

            // 注册表取装配后的实例：内容须先就绪，展示键校验才看得到 mod 注册进来的条目。
            // 资源包挂载作为委托交进装配过程，次序由 ModAssets.Assemble 保证：装载展示代码 → 挂载 → 入口执行
            var assets = ModAssets.Assemble(
                catalog, boot.Registry,
                MountAssetPacks,
                ServiceLocator.LoggerFactory);
            ServiceLocator.ModAssets = assets;

            LogDisplayCoverage(boot.Registry, assets.Registry);
        }
        catch (Exception ex) {
            // 装配中断不吞也不留中间态：补上装配层上下文后继续上抛，幂等标志保持未置位
            throw new InvalidOperationException("mod 装配失败，内容未就绪。", ex);
        }

        _initialized = true;
    }

    /// <summary>
    /// 展示覆盖巡检：取内容条目对应的展示数据，无展示条目者汇总记一条告警，并记一条装配规模。
    /// 缺席不阻断装配——消费方按内容键回退显示名、图标留空、无范围提示与环境场景。
    /// 规模按内容条目数报，不报展示条目数：mod 可声明内容里没有的展示键，那部分不进内容计数。
    /// </summary>
    private static void LogDisplayCoverage(ContentSetRegistry registry, IDisplayRegistry display) {
        var missing =
            registry.Skills.Where(skill => display.GetSkill(skill.SkillId) is null)
                .Select(skill => $"技能 {skill.SkillId.Id}")
            .Concat(registry.Buffs.Where(buff => display.GetBuff(buff.BuffTypeId) is null)
                .Select(buff => $"Buff {buff.BuffTypeId.Value}"))
            .Concat(registry.Units.Where(unit => display.GetUnit(unit.ConfigKey) is null)
                .Select(unit => $"单位 {unit.ConfigKey.Value}"))
            .Concat(registry.Dungeons.Where(dungeon => display.GetDungeon(dungeon.DungeonKey) is null)
                .Select(dungeon => $"副本 {dungeon.DungeonKey.Value}"))
            .ToList();

        if (Logger.IsEnabled(LogLevel.Information))
            Logger.LogInformation("展示装配完成：内容技能 {Skills}/Buff {Buffs}/单位 {Units}/副本 {Dungeons}，无展示 {Missing}",
                registry.Skills.Count, registry.Buffs.Count, registry.Units.Count, registry.Dungeons.Count,
                missing.Count);

        if (missing.Count > 0 && Logger.IsEnabled(LogLevel.Warning))
            Logger.LogWarning("以下条目无展示数据，按内容键降级展示：{Entries}", string.Join("、", missing));
    }

    /// <summary>逐 mod 挂载它声明的展示资源包，并把该 mod 展示程序集里的脚本类注册进 Godot 脚本系统。
    /// 挂载失败只记日志，不中止装配。包内资源以 mod 导出时固化的 <c>res://mods/{mod id}/</c> 前缀寻址，由 mod 侧自行加载。
    /// 注册必须赶在入口执行之前：包内 <c>.tres</c>/<c>.tscn</c> 引用的脚本类若不注册，实例化时按类型查找失败。</summary>
    private static void MountAssetPacks(IReadOnlyList<ModDisplayDeclaration> displays) {
        foreach (var display in displays) {
            foreach (string pck in display.ResourcePacks) {
                // 声明即承诺存在，缺席在这里才判：只有客户端目录里才有展示资源包，扫描期不能据此拒载
                if (ProjectSettings.LoadResourcePack(Path.GetFullPath(pck))) {
                    if (Logger.IsEnabled(LogLevel.Information))
                        Logger.LogInformation("已挂载展示资源包：{Pck}", pck);
                }
                else {
                    Logger.LogError("展示资源包挂载失败：{Pck}", pck);
                }
            }

            RegisterDisplayScripts(display);
        }
    }

    /// <summary>
    /// 把该 mod 展示程序集里的脚本类注册进 Godot 脚本系统。
    /// 展示 DLL 走独立 <c>AssemblyLoadContext</c> 装载，Godot 不会自行发现其中的脚本类；
    /// 按程序集名匹配进程内已加载副本——展示 DLL 只可能由本次装配的装载器装载。
    /// </summary>
    private static void RegisterDisplayScripts(ModDisplayDeclaration display) {
        foreach (string dll in display.EntryDlls) {
            string assemblyName = Path.GetFileNameWithoutExtension(dll);
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));
            if (assembly is null) {
                Logger.LogError("展示程序集未在进程中加载，包内脚本类不会注册：{ModId} {Dll}",
                    display.ModId, dll);
                continue;
            }

            ScriptManagerBridge.LookupScriptsInAssembly(assembly);
        }
    }
}
