using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Battle.Mod;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// 展示资源获取入口：mod 管理、展示注册表与展示装配的持有者，装配一次全程只读。
/// 静态组合根，与 <c>ServiceLocator</c> 同构——项目不引 DI 容器。
/// 未装配时 <see cref="Registry"/> 是空索引、查询恒返回 null 而不抛，保证零 mod 环境直接可用。
/// </summary>
/// <remarks>
/// 装配分两步：<see cref="Initialize"/> 把内置展示数据注册进调用方给出的注册表，
/// 再逐 mod 装载展示代码 DLL 并把入口注册进同一注册表；宿主把它接进自己的资源体系后
/// 再 <see cref="Publish"/> 发布最终索引。分两步是因为可被 <c>.tres</c>/<c>.tscn</c>
/// 引用的资源类只能留在 Godot 主程序集，mod 视图到资源对象的落地必须由宿主完成。
/// </remarks>
public static class ModAssets {
    /// <summary>展示代码子目录名，客户端 ALC 装载 mod 展示程序集使用；服务端不加载。</summary>
    public const string DisplayCodeDirectoryName = "code_display";

    private static DisplayRegistry _registry = new();

    /// <summary>当前展示索引；装配后为内置与 mod 的合并结果。</summary>
    public static IDisplayRegistry Registry => _registry;

    /// <summary>mod 管理根；未装配为 null。</summary>
    public static ModCatalog? Catalog {
        get; private set;
    }

    /// <summary>装配那一刻的启用集指纹；与 <see cref="ModCatalog.Fingerprint"/> 不等即说明磁盘已改动而未重启。</summary>
    public static string AssemblyFingerprint {
        get; private set;
    } = "";

    /// <summary>
    /// 展示装配：先注册内置展示数据（调用方已做），再逐 mod 装载展示代码 DLL，
    /// 经 <see cref="IModDisplayEntry.Initialize"/> 把资源与视图注册进 <paramref name="registry"/>。
    /// 返回 mod 侧注册面记录，供宿主判定 mod 覆盖与桥接资源对象。
    /// </summary>
    /// <param name="catalog">已扫描的 mod 管理根，提供参与装配的启用 mod 与错误落点。</param>
    /// <param name="content">装配好的内容注册表，展示键完整性校验对它做。</param>
    /// <param name="registry">待填充的展示注册表。</param>
    /// <param name="modsRootGodotPath">mods 根目录在 Godot 路径体系下的挂载点，null 即不支持 mod 自带场景。</param>
    public static ModDisplayRuntime Initialize(
        ModCatalog catalog, ContentSetRegistry content, DisplayRegistry registry, string? modsRootGodotPath = null) {
        var errors = new List<string>();
        var runtime = new ModDisplayRuntime(registry, content, errors);
        var loader = new ModResourceLoader(catalog.ModsRootPath, modsRootGodotPath);

        foreach (var mod in catalog.EnabledMods)
            LoadDisplayAssemblies(mod, runtime, loader, errors);

        catalog.DisplayErrors = errors;
        Catalog = catalog;
        AssemblyFingerprint = catalog.Fingerprint;
        return runtime;
    }

    private static void LoadDisplayAssemblies(
        LoadedMod mod, IModDisplayRuntime runtime, ModResourceLoader loader, List<string> errors) {
        string displayDir = Path.Combine(mod.DirectoryPath, DisplayCodeDirectoryName);
        if (!Directory.Exists(displayDir))
            return; // 无展示代码的 mod 只贡献数据面

        foreach (string dll in Directory.GetFiles(displayDir, "*.dll", SearchOption.TopDirectoryOnly)) {
            try {
                using var moduleLoader = new ModAssemblyLoader($"mod_display_{mod.Manifest.Id}");
                moduleLoader.AddDependencyDirectory(displayDir);
                var context = new ModDisplayContext(mod.Manifest.Id, loader);
                moduleLoader.LoadEntry<IModDisplayEntry>(dll)?.Initialize(runtime, context);
            }
            catch (Exception ex) {
                errors.Add($"{mod.Manifest.Id}: 展示代码入口装载失败 {Path.GetFileName(dll)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 发布最终展示索引。宿主把 mod 视图落地成自己的资源对象后调用一次。
    /// </summary>
    public static void Publish(DisplayRegistry registry) => _registry = registry;

    /// <summary>按技能键取展示视图；未注册返回 null。</summary>
    public static ISkillView? Skill(string skillKey) => _registry.GetSkill(skillKey);

    /// <summary>按 BuffTypeId 取展示视图；未注册返回 null。</summary>
    public static IBuffView? Buff(ushort buffTypeId) => _registry.GetBuff(buffTypeId);

    /// <summary>按副本键取展示视图；未注册返回 null。</summary>
    public static IDungeonView? Dungeon(string? dungeonKey) => _registry.GetDungeon(dungeonKey);

    /// <summary>按单位配置键取展示视图；未注册返回 null。</summary>
    public static IUnitView? Unit(string configKey) => _registry.GetUnit(configKey);

    /// <summary>按资源名取纹理；名未注册或解析失败返回 null。</summary>
    public static Texture2D? Texture(string? assetId) => _registry.Texture(assetId);

    /// <summary>按资源名取场景模板；名未注册或解析失败返回 null。</summary>
    public static PackedScene? Scene(string? assetId) => _registry.Scene(assetId);

    /// <summary>
    /// 启停一个 mod 并落盘启用集。内容装配是一次性的，返回后需重启进程新启用集才生效。
    /// </summary>
    public static bool SetEnabled(string modId, bool enabled) => Catalog?.SetEnabled(modId, enabled) ?? false;
}
