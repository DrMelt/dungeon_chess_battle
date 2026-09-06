using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// mod 展示装配面：包装 <see cref="DisplayRegistry"/> 递给展示代码入口，
/// 记录本 mod 声明过的展示键（供宿主判定 mod 覆盖而非内置模板），
/// 并校验展示引用的内容键确实存在于内容注册表——引用不存在的技能/Buff/单位/副本听其注册并记错误，不中断其余 mod。
/// </summary>
public sealed class ModDisplayRuntime(DisplayRegistry registry, ContentSetRegistry content, List<string> errors)
    : IModDisplayRuntime {
    /// <summary>被包装的展示注册表，宿主落地资源时读内置与 mod 合并后的视图。</summary>
    public DisplayRegistry DisplayRegistry => registry;

    /// <summary>被 mod 声明过的技能键。</summary>
    public HashSet<string> Skills { get; } = new(StringComparer.Ordinal);

    /// <summary>被 mod 声明过的 BuffTypeId。</summary>
    public HashSet<ushort> Buffs { get; } = [];

    /// <summary>被 mod 声明过的单位配置键。</summary>
    public HashSet<string> Units { get; } = new(StringComparer.Ordinal);

    /// <summary>被 mod 声明过的副本键。</summary>
    public HashSet<string> Dungeons { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void RegisterTexture(string id, Func<Texture2D?> provider) => registry.RegisterTexture(id, provider);

    /// <inheritdoc/>
    public void RegisterScene(string id, Func<PackedScene?> provider) => registry.RegisterScene(id, provider);

    /// <inheritdoc/>
    public void RegisterSkill(ISkillView view) {
        if (content.GetSkill(new SkillKeyId(view.Id)) is null)
            errors.Add($"展示引用未知技能 '{view.Id}'");
        registry.RegisterSkill(view);
        Skills.Add(view.Id);
    }

    /// <inheritdoc/>
    public void RegisterBuff(IBuffView view) {
        if (view.BuffTypeId != 0 && content.GetBuff(view.BuffTypeId) is null)
            errors.Add($"展示引用未知 Buff（BuffTypeId）'{view.BuffTypeId}'");
        registry.RegisterBuff(view);
        Buffs.Add(view.BuffTypeId);
    }

    /// <inheritdoc/>
    public void RegisterUnit(IUnitView view) {
        if (content.GetUnit(new UnitConfigKey(view.ConfigKey)) is null)
            errors.Add($"展示引用未知单位 '{view.ConfigKey}'");
        registry.RegisterUnit(view);
        Units.Add(view.ConfigKey);
    }

    /// <inheritdoc/>
    public void RegisterDungeon(IDungeonView view) {
        if (content.GetDungeon(view.Key) is null)
            errors.Add($"展示引用未知副本 '{view.Key}'");
        registry.RegisterDungeon(view);
        Dungeons.Add(view.Key);
    }
}
