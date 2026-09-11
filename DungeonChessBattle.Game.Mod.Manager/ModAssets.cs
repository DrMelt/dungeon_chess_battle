using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Mod.Interface;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;

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
    /// <param name="catalog">已扫描的 mod 管理根，提供参与装配的启用 mod 与错误落点。</param>
    /// <param name="content">内容注册表只读视图，展示键完整性校验对它做。</param>
    /// <param name="mountResourcePacks">逐 mod 挂载它声明的展示资源包，必须介于展示代码装载与入口执行之间。</param>
    /// <param name="applyModResources">把 mod 声明过的条目落地成宿主资源对象，收 mod 声明集与注册表。</param>
    /// <returns>装配完成的获取入口实例，由调用方持有。</returns>
    public static ModAssets Assemble(
        ModCatalog catalog,
        IContentRegistryView content,
        Action<IReadOnlyList<LoadedMod>> mountResourcePacks,
        Action<ModDeclaration, DisplayRegistry> applyModResources) {
        var registry = new DisplayRegistry();

        var runtimeErrors = new List<ModError>();
        var declared = new ModDeclaration();

        // 装载展示代码不执行入口，ALC 由 loaded 持有到入口统一执行完成
        using var loaded = ModEntryLoader.Load<IModDisplayEntry>(
            catalog.EnabledMods, ModCodeKind.Display, "mod_display_", "展示代码入口装载失败");

        mountResourcePacks(catalog.EnabledMods);

        var initErrors = ModEntryLoader.Initialize(loaded, "展示代码入口执行失败",
            (entry, mod) => entry.Initialize(
                new ModDisplayRuntime(registry, content, runtimeErrors, mod.Manifest.Id, declared),
                new ModDisplayContext(mod.Manifest.Id, registry)));
        catalog.RecordDisplayErrors([.. loaded.Errors, .. runtimeErrors, .. initErrors]);

        applyModResources(declared, registry);

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
}
