using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 展示注册表：<see cref="IDisplayRegistry"/> 条目读面与 <see cref="IModDisplayRuntime"/> 条目写面的实现。
/// 条目数据同键后写覆盖，且未声明字段沿用被覆盖者，因此后到的注册者只换图标不会清空先到的名称。
/// 装配在启动期单线程完成，之后只读查询，无锁。
/// </summary>
public sealed class DisplayRegistry : IModDisplayRuntime, IDisplayRegistry {
    private readonly Dictionary<SkillKeyId, SkillDisplay> _skills = [];
    private readonly Dictionary<BuffTypeId, BuffDisplay> _buffs = [];
    private readonly Dictionary<UnitConfigKey, UnitDisplay> _units = [];
    private readonly Dictionary<DungeonKeyId, DungeonDisplay> _dungeons = [];

    /// <inheritdoc/>
    public void RegisterSkill(SkillDisplay display) {
        if (display.SkillId.IsDefault)
            return;
        _skills[display.SkillId] = _skills.TryGetValue(display.SkillId, out var prev)
            ? prev.Merge(display)
            : display;
    }

    /// <inheritdoc/>
    public void RegisterBuff(BuffDisplay display) {
        if (display.BuffTypeId.IsDefault)
            return;
        _buffs[display.BuffTypeId] = _buffs.TryGetValue(display.BuffTypeId, out var prev)
            ? prev.Merge(display)
            : display;
    }

    /// <inheritdoc/>
    public void RegisterUnit(UnitDisplay display) {
        if (display.ConfigKey.IsDefault)
            return;
        _units[display.ConfigKey] = _units.TryGetValue(display.ConfigKey, out var prev)
            ? prev.Merge(display)
            : display;
    }

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonDisplay display) {
        if (display.DungeonKey.IsDefault)
            return;
        _dungeons[display.DungeonKey] = _dungeons.TryGetValue(display.DungeonKey, out var prev)
            ? prev.Merge(display)
            : display;
    }

    /// <inheritdoc/>
    public SkillDisplay? GetSkill(SkillKeyId skillId) => _skills.GetValueOrDefault(skillId);

    /// <inheritdoc/>
    public BuffDisplay? GetBuff(BuffTypeId buffTypeId) => _buffs.GetValueOrDefault(buffTypeId);

    /// <inheritdoc/>
    public DungeonDisplay? GetDungeon(DungeonKeyId dungeonKey) => _dungeons.GetValueOrDefault(dungeonKey);

    /// <inheritdoc/>
    public UnitDisplay? GetUnit(UnitConfigKey configKey) => _units.GetValueOrDefault(configKey);
}
