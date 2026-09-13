using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Mod.Interface;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 条目展示数据获取入口：展示注册表与 mod 管理根的持有者，一次装配产出，全程只读。
/// 本库不持全局状态，实例由宿主工程的组合根持有。
/// 未注册的键查询恒返回 null 而不抛，保证零 mod 环境直接可用。
/// </summary>
public sealed class ModAssets {
    private readonly DisplayRegistry _registry;

    private ModAssets(DisplayRegistry registry, ModCatalog catalog, string assemblyFingerprint) {
        _registry = registry;
        Catalog = catalog;
        AssemblyFingerprint = assemblyFingerprint;
    }

    /// <summary>本次装配所用的 mod 管理根。</summary>
    public ModCatalog Catalog {
        get;
    }

    /// <summary>装配那一刻的启用集指纹；与 <see cref="ModCatalog.Fingerprint"/> 不等即说明磁盘已改动而未重启。</summary>
    public string AssemblyFingerprint {
        get;
    }

    /// <summary>
    /// 展示装配全过程，顺序由本方法保证：建注册表 → 装载展示代码 → 挂载展示资源包 →
    /// 逐 mod 执行入口把声明注册进同表 → 宿主把 mod 声明落地成资源对象并回注 → 表就绪后对外可查。
    /// </summary>
    /// <remarks>
    /// 资源包挂载与 mod 条目落地两步以委托交入：可被 <c>.tres</c>/<c>.tscn</c> 引用的资源类与
    /// <c>res://</c> 路径只能留在 Godot 主工程，本库不认识它们，只负责把顺序钉死在这里。
    /// 装载先于挂载、入口执行后于挂载：入口 Initialize 要按 <c>res://mods/{id}/</c> 前缀自行读包内资源。
    /// </remarks>
    /// <param name="catalog">已扫描的 mod 管理根，提供参与装配的启用 mod、展示声明与错误落点。</param>
    /// <param name="content">内容注册表只读视图，展示键完整性校验对它做。</param>
    /// <param name="mountResourcePacks">逐 mod 挂载它声明的展示资源包，必须介于展示代码装载与入口执行之间。</param>
    /// <param name="applyModResources">把 mod 声明过的条目落地成宿主资源对象，收 mod 声明集与注册表。</param>
    /// <param name="loggerFactory">日志通道工厂，未注入时静默。</param>
    /// <returns>装配完成的获取入口实例，由调用方持有。</returns>
    public static ModAssets Assemble(
        ModCatalog catalog,
        IContentRegistryView content,
        Action<IReadOnlyList<ModDisplayDeclaration>> mountResourcePacks,
        Action<ModDeclaration, DisplayRegistry> applyModResources,
        ILoggerFactory? loggerFactory = null) {
        var logger = loggerFactory?.CreateLogger<ModAssets>() ?? NullLogger<ModAssets>.Instance;
        var registry = new DisplayRegistry();

        var runtimeErrors = new List<ModError>();
        var declared = new ModDeclaration();

        // 声明不可用的 mod 不参与展示装配：数据面已按同一份清单照常装载，展示面缺一块不影响它
        var displays = catalog.EnabledDisplays.Where(display => display.Problem is null).ToList();
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("展示装配开始：启用 {Count} 个 mod，展示声明不可用 {Blocked} 个",
                catalog.EnabledMods.Count, catalog.EnabledDisplays.Count - displays.Count);

        // 装载展示代码不执行入口，ALC 由 loaded 持有到入口统一执行完成
        var sources = displays
            .Select(display => new ModEntrySource(display.ModId, display.EntryDlls, display.ProbeDirectories))
            .ToList();
        using var loaded = ModEntryLoader.Load<IModDisplayEntry>(
            sources, "mod_display_", "展示代码入口装载失败", loggerFactory);

        mountResourcePacks(displays);

        var initErrors = ModEntryLoader.Initialize(loaded, "展示代码入口执行失败",
            (entry, modId) => entry.Initialize(
                new ModDisplayRuntime(registry, content, runtimeErrors, modId, declared),
                new ModDisplayContext(modId, registry)),
            loggerFactory);

        // 展示问题四处产出，收敛到装配结束处落日志一次，条目仍按归属进 catalog
        foreach (var error in loaded.Errors.Concat(runtimeErrors).Concat(initErrors))
            LogDisplayFailed(logger, error);
        catalog.RecordDisplayErrors([.. loaded.Errors, .. runtimeErrors, .. initErrors]);

        applyModResources(declared, registry);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("展示装配完成：mod 声明 技能 {Skills}/Buff {Buffs}/单位 {Units}/副本 {Dungeons}",
                declared.Skills.Count, declared.Buffs.Count, declared.Units.Count, declared.Dungeons.Count);

        return new ModAssets(registry, catalog, catalog.Fingerprint);
    }

    /// <summary>按技能键取展示数据；未注册返回 null。</summary>
    public SkillDisplay? Skill(SkillKeyId skillId) => _registry.GetSkill(skillId);

    /// <summary>按 Buff 键取展示数据；未注册返回 null。</summary>
    public BuffDisplay? Buff(BuffTypeId buffTypeId) => _registry.GetBuff(buffTypeId);

    /// <summary>按副本键取展示数据；未注册返回 null。</summary>
    public DungeonDisplay? Dungeon(DungeonKeyId dungeonKey) => _registry.GetDungeon(dungeonKey);

    /// <summary>按单位配置键取展示数据；未注册返回 null。</summary>
    public UnitDisplay? Unit(UnitConfigKey configKey) => _registry.GetUnit(configKey);

    /// <summary>一条展示装配问题：原因已含 mod 归属，条目同时进管理面。</summary>
    private static void LogDisplayFailed(ILogger logger, ModError error) {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError("展示装配失败：{ModId}：{Reason}", error.ModId, error.Message);
    }
}
