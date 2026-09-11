using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 展示注册表：<see cref="IDisplayRegistry"/> 场景资源口兼条目读面与 <see cref="IModDisplayRuntime"/> 条目写面的实现。
/// 场景名以取供器登记、首次查询时才解析并缓存结果，令跨 mod 引用不受包注册次序影响；
/// 条目数据同键后写覆盖，且未声明字段沿用被覆盖者，因此后到的注册者只换图标不会清空先到的名称。
/// 装配在启动期单线程完成，之后只读查询，无锁。
/// </summary>
public sealed class DisplayRegistry : IModDisplayRuntime, IDisplayRegistry {
    private readonly Dictionary<string, Func<PackedScene?>> _sceneProviders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackedScene?> _scenes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillDisplay> _skills = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BuffDisplay> _buffs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitDisplay> _units = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DungeonDisplay> _dungeons = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void RegisterScene(string id, Func<PackedScene?> provider) => _sceneProviders[id] = provider;

    /// <inheritdoc/>
    public void RegisterSkill(SkillDisplay display) =>
        _skills[display.Id] = _skills.TryGetValue(display.Id, out var prev) ? prev.Merge(display) : display;

    /// <inheritdoc/>
    public void RegisterBuff(BuffDisplay display) {
        if (string.IsNullOrEmpty(display.BuffTypeId))
            return;
        _buffs[display.BuffTypeId] = _buffs.TryGetValue(display.BuffTypeId, out var prev)
            ? prev.Merge(display)
            : display;
    }

    /// <inheritdoc/>
    public void RegisterUnit(UnitDisplay display) =>
        _units[display.ConfigKey] = _units.TryGetValue(display.ConfigKey, out var prev)
            ? prev.Merge(display)
            : display;

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonDisplay display) =>
        _dungeons[display.Key] = _dungeons.TryGetValue(display.Key, out var prev)
            ? prev.Merge(display)
            : display;

    /// <inheritdoc/>
    public SkillDisplay? GetSkill(string skillKey) => _skills.GetValueOrDefault(skillKey);

    /// <inheritdoc/>
    public BuffDisplay? GetBuff(string buffKey) => _buffs.GetValueOrDefault(buffKey);

    /// <inheritdoc/>
    public DungeonDisplay? GetDungeon(string? dungeonKey) =>
        string.IsNullOrWhiteSpace(dungeonKey) ? null : _dungeons.GetValueOrDefault(dungeonKey);

    /// <inheritdoc/>
    public UnitDisplay? GetUnit(string configKey) => _units.GetValueOrDefault(configKey);

    /// <inheritdoc/>
    public PackedScene? Scene(string? assetId) {
        if (string.IsNullOrEmpty(assetId) || !_sceneProviders.TryGetValue(assetId, out Func<PackedScene?>? provider))
            return null;
        if (!_scenes.TryGetValue(assetId, out PackedScene? scene)) {
            scene = provider();
            _scenes[assetId] = scene;
        }

        return scene;
    }
}
