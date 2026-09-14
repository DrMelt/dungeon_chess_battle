using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonChessBattle.Battle.Config.Registry;
using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GameAssets.Mods;
using DungeonChessBattle.Game.Mod.Manager;
using Godot;
using Godot.Bridge;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// Godot 端 mod 装配编排：扫描启用集 → 挂载各 mod 资源包 → 数据面装配 → 展示面装配。
/// 主场景 _Ready 首个调用，保证任何 UI 与资源表访问前内容已就绪；
/// 服务器子进程由 ServerProcessHost 注入同一 user://mods，两端读同一启用集与内容即同源。
/// 两次装配的产物分别写入 <see cref="ServiceLocator.GameContent"/> 与 <see cref="ServiceLocator.ModAssets"/>，
/// 展示装配内部的先后次序不在这里，见 <see cref="ModAssets.Assemble"/>。
/// </summary>
public static class ModManager {
    /// <summary>mods 根目录的 Godot 路径，指向 user:// 下存放 mod 包的目录。</summary>
    // Godot user:// 虚拟路径，非文件系统绝对路径，S1075 误报
#pragma warning disable S1075
    public const string ModsRootGodotPath = "user://mods";
#pragma warning restore S1075

    /// <summary>mods 根目录绝对路径。</summary>
    public static string ModsRootPath => ProjectSettings.GlobalizePath(ModsRootGodotPath);

    private static readonly ILogger Logger = ServiceLocator.CreateLogger(nameof(ModManager));

    private static bool _initialized;

    /// <summary>
    /// 执行一次装配，幂等；单个 mod 失败不中止其余 mod，错误汇总进装配产物的 <c>ModCatalog</c>。
    /// 扫描、装配与展示装配的过程日志由下层按自身类别名记录，本类只注入通道并补记 Godot 侧落地结果。
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
            // 数据面产物交组合根持有，UI 与资源表经 ServiceLocator.GameContent 取数
            ServiceLocator.BindContent(boot.Content);

            // 注册表取装配后的实例：内容须先就绪，展示键校验才看得到 mod 注册进来的条目。
            // 资源包挂载作为委托交进装配过程，次序由 ModAssets.Assemble 保证：装载展示代码 → 挂载 → 入口执行
            ServiceLocator.ModAssets = ModAssets.Assemble(
                catalog, boot.Content.Registry,
                MountAssetPacks,
                (declared, registry) => ModAssetsMapper.Apply(boot.Content.Registry, declared, registry),
                ServiceLocator.LoggerFactory);

            // 表内条目落在 ModAssetsMapper 里，只有这里看得到三张表的最终条数
            if (Logger.IsEnabled(LogLevel.Information))
                Logger.LogInformation("落地展示条目：技能 {Skills}/Buff {Buffs}/副本 {Dungeons}",
                    ResourceTables.Skills.AllResources.Count, ResourceTables.Buffs.AllResources.Count,
                    ResourceTables.Dungeons.AllResources.Count);
        }
        catch (Exception ex) {
            // 装配中断不吞也不留中间态：补上装配层上下文后继续上抛，幂等标志保持未置位
            throw new InvalidOperationException("mod 装配失败，内容未就绪。", ex);
        }

        _initialized = true;
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
