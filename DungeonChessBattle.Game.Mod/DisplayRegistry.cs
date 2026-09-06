using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// 展示注册表：注册面与查询面的共同实现。资源以取供器登记、首次查询时才解析并缓存结果，
/// 令跨 mod 引用不受包注册次序影响；条目视图同键后注册覆盖先注册，且未声明字段沿用被覆盖者，
/// 因此 mod 只换图标不会把内置名称一并清空。
/// 装配在启动期单线程完成，之后只读查询，无锁。
/// </summary>
public sealed class DisplayRegistry : IModDisplayRuntime, IDisplayRegistry {
    private readonly Dictionary<string, Func<Texture2D?>> _textureProviders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<PackedScene?>> _sceneProviders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackedScene?> _scenes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ISkillView> _skills = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, IBuffView> _buffs = [];
    private readonly Dictionary<string, IUnitView> _units = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IDungeonView> _dungeons = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void RegisterTexture(string id, Func<Texture2D?> provider) => _textureProviders[id] = provider;

    /// <inheritdoc/>
    public void RegisterScene(string id, Func<PackedScene?> provider) => _sceneProviders[id] = provider;

    /// <inheritdoc/>
    public void RegisterSkill(ISkillView view) =>
        _skills[view.Id] = _skills.TryGetValue(view.Id, out var prev) ? SkillViewMerge.Merge(prev, view) : view;

    /// <inheritdoc/>
    public void RegisterBuff(IBuffView view) {
        if (view.BuffTypeId == 0)
            return;
        _buffs[view.BuffTypeId] = _buffs.TryGetValue(view.BuffTypeId, out var prev)
            ? BuffViewMerge.Merge(prev, view)
            : view;
    }

    /// <inheritdoc/>
    public void RegisterUnit(IUnitView view) =>
        _units[view.ConfigKey] = _units.TryGetValue(view.ConfigKey, out var prev)
            ? UnitViewMerge.Merge(prev, view)
            : view;

    /// <inheritdoc/>
    public void RegisterDungeon(IDungeonView view) =>
        _dungeons[view.Key] = _dungeons.TryGetValue(view.Key, out var prev)
            ? DungeonViewMerge.Merge(prev, view)
            : view;

    /// <inheritdoc/>
    public ISkillView? GetSkill(string skillKey) => _skills.GetValueOrDefault(skillKey);

    /// <inheritdoc/>
    public IBuffView? GetBuff(ushort buffTypeId) => _buffs.GetValueOrDefault(buffTypeId);

    /// <inheritdoc/>
    public IDungeonView? GetDungeon(string? dungeonKey) =>
        string.IsNullOrWhiteSpace(dungeonKey) ? null : _dungeons.GetValueOrDefault(dungeonKey);

    /// <inheritdoc/>
    public IUnitView? GetUnit(string configKey) => _units.GetValueOrDefault(configKey);

    /// <inheritdoc/>
    public Texture2D? Texture(string? assetId) => Resolve(_textureProviders, _textures, assetId);

    /// <inheritdoc/>
    public PackedScene? Scene(string? assetId) => Resolve(_sceneProviders, _scenes, assetId);

    /// <summary>该纹理资源名是否已注册；供装配期校验条目引用。</summary>
    public bool HasTexture(string id) => _textureProviders.ContainsKey(id);

    /// <summary>该场景资源名是否已注册；供装配期校验条目引用。</summary>
    public bool HasScene(string id) => _sceneProviders.ContainsKey(id);

    private static T? Resolve<T>(
        Dictionary<string, Func<T?>> providers, Dictionary<string, T?> resolved, string? assetId)
        where T : class {
        if (string.IsNullOrEmpty(assetId) || !providers.TryGetValue(assetId, out Func<T?>? provider))
            return null;
        if (!resolved.TryGetValue(assetId, out T? value)) {
            value = provider();
            resolved[assetId] = value;
        }
        return value;
    }
}
