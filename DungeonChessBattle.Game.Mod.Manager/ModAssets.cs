using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Game.Mod.Interface;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 展示资源获取入口：展示注册表与 mod 管理的持有者，装配一次全程只读。
/// 静态组合根，与 <c>ServiceLocator</c> 同构——项目不引 DI 容器。
/// 未装配时查询恒返回 null 而不抛，保证零 mod 环境直接可用。
/// </summary>
public static class ModAssets {
    private static DisplayRegistry _registry = new();

    /// <summary>mod 管理根；未装配为 null。</summary>
    public static ModCatalog? Catalog {
        get; private set;
    }

    /// <summary>装配那一刻的启用集指纹；与 <see cref="ModCatalog.Fingerprint"/> 不等即说明磁盘已改动而未重启。</summary>
    public static string AssemblyFingerprint {
        get; private set;
    } = "";

    /// <summary>
    /// 展示装配全过程，顺序由本方法保证：建注册表 → 引擎侧先入表 → 装载展示代码 → 挂载展示资源包 →
    /// 逐 mod 执行入口把声明注册进同表 → 宿主把 mod 声明落地成资源对象并回注 → 表就绪后对外可查。
    /// </summary>
    /// <remarks>
    /// 引擎侧注册、资源包挂载与 mod 条目落地三步以委托交入：可被 <c>.tres</c>/<c>.tscn</c> 引用的资源类与
    /// <c>res://</c> 路径只能留在 Godot 主工程，本库不认识它们，只负责把顺序钉死在这里。
    /// 装载先于挂载、入口执行后于挂载：入口 Initialize 要经 <c>res://mods/{id}/</c> 读包内资源，
    /// 而契约程序集必须宿主已装载（引擎侧注册已保证）。
    /// </remarks>
    /// <param name="catalog">已扫描的 mod 管理根，提供参与装配的启用 mod 与错误落点。</param>
    /// <param name="content">内容注册表只读视图，展示键完整性校验对它做。</param>
    /// <param name="registerBuiltin">把引擎预置场景名与单位外观占位注册进注册表，必须先于 mod 声明。</param>
    /// <param name="mountResourcePacks">逐 mod 挂载它声明的展示资源包，必须介于展示代码装载与入口执行之间。</param>
    /// <param name="applyModResources">把 mod 声明过的条目落地成宿主资源对象，收 mod 声明集与注册表。</param>
    /// <param name="modsRootGodotPath">mods 根目录在 Godot 路径体系下的挂载点，null 即读不到包内 PCK 资源。</param>
    public static void Assemble(
        ModCatalog catalog,
        IContentRegistryView content,
        Action<IModDisplayRuntime> registerBuiltin,
        Action<IReadOnlyList<LoadedMod>> mountResourcePacks,
        Action<ModDeclaration, DisplayRegistry> applyModResources,
        string? modsRootGodotPath = null) {
        var registry = new DisplayRegistry();
        registerBuiltin(registry);

        var runtimeErrors = new List<ModError>();
        var declared = new ModDeclaration();
        var loader = new ModResourceLoader(catalog.ModsRootPath, modsRootGodotPath);

        // 装载展示代码不执行入口，ALC 由 loaded 持有到入口统一执行完成
        using var loaded = ModEntryLoader.Load<IModDisplayEntry>(
            catalog.EnabledMods, ModCodeKind.Display, "mod_display_", "展示代码入口装载失败");

        mountResourcePacks(catalog.EnabledMods);

        var initErrors = ModEntryLoader.Initialize(loaded, "展示代码入口执行失败",
            (entry, mod) => entry.Initialize(
                new ModDisplayRuntime(registry, content, runtimeErrors, mod.Manifest.Id, declared),
                new ModDisplayContext(mod.Manifest.Id, loader, registry)));
        catalog.RecordDisplayErrors([.. loaded.Errors, .. runtimeErrors, .. initErrors]);

        applyModResources(declared, registry);

        Catalog = catalog;
        AssemblyFingerprint = catalog.Fingerprint;
        _registry = registry;
    }

    /// <summary>按技能键取展示数据；未注册返回 null。</summary>
    public static SkillDisplay? Skill(string skillKey) => _registry.GetSkill(skillKey);

    /// <summary>按 BuffTypeId 取展示数据；未注册返回 null。</summary>
    public static BuffDisplay? Buff(ushort buffTypeId) => _registry.GetBuff(buffTypeId);

    /// <summary>按副本键取展示数据；未注册返回 null。</summary>
    public static DungeonDisplay? Dungeon(string? dungeonKey) => _registry.GetDungeon(dungeonKey);

    /// <summary>按单位配置键取展示数据；未注册返回 null。</summary>
    public static UnitDisplay? Unit(string configKey) => _registry.GetUnit(configKey);

    /// <summary>按资源名取纹理；名未注册或解析失败返回 null。</summary>
    public static Texture2D? Texture(string? assetId) => _registry.Texture(assetId);

    /// <summary>按资源名取场景模板；名未注册或解析失败返回 null。</summary>
    public static PackedScene? Scene(string? assetId) => _registry.Scene(assetId);

    /// <summary>
    /// 启停一个 mod 并落盘启用集。内容装配是一次性的，返回后需重启进程新启用集才生效。
    /// </summary>
    public static bool SetEnabled(string modId, bool enabled) => Catalog?.SetEnabled(modId, enabled) ?? false;
}
