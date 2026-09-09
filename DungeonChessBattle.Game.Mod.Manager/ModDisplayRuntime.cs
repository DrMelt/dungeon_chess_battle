using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// mod 侧声明过的展示键全集，跨 mod 汇总。宿主据此判定条目是否被 mod 覆盖、
/// 该以已落地的资源为模板改写还是直接补占位。
/// </summary>
public sealed class ModDeclaration {
    /// <summary>被 mod 声明过的技能键。</summary>
    public HashSet<string> Skills { get; } = new(StringComparer.Ordinal);

    /// <summary>被 mod 声明过的 BuffTypeId。</summary>
    public HashSet<ushort> Buffs { get; } = [];

    /// <summary>被 mod 声明过的单位配置键。</summary>
    public HashSet<string> Units { get; } = new(StringComparer.Ordinal);

    /// <summary>被 mod 声明过的副本键。</summary>
    public HashSet<string> Dungeons { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// mod 展示装配面：一个 mod 一个实例，包装 <see cref="DisplayRegistry"/> 递给该 mod 的展示代码入口。
/// 声明过的展示键记进跨 mod 汇总的 <see cref="ModDeclaration"/>，错误按归属 mod 记录，
/// 并按只读视图校验展示引用的内容键存在于内容注册表——不存在则记错误，不中断其余 mod。
/// </summary>
public sealed class ModDisplayRuntime(
    DisplayRegistry registry,
    IContentRegistryView content,
    List<ModError> errors,
    string modId,
    ModDeclaration declared) : IModDisplayRuntime {
    /// <inheritdoc/>
    public void RegisterTexture(string id, Func<Texture2D?> provider) => registry.RegisterTexture(id, provider);

    /// <inheritdoc/>
    public void RegisterScene(string id, Func<PackedScene?> provider) => registry.RegisterScene(id, provider);

    /// <inheritdoc/>
    public void RegisterSkill(SkillDisplay display) {
        if (content.GetSkill(new SkillKeyId(display.Id)) is null)
            errors.Add(new ModError(modId, $"展示引用未知技能 '{display.Id}'"));
        registry.RegisterSkill(display);
        declared.Skills.Add(display.Id);
    }

    /// <inheritdoc/>
    public void RegisterBuff(BuffDisplay display) {
        if (display.BuffTypeId == 0)
            return;
        if (content.GetBuff(display.BuffTypeId) is null)
            errors.Add(new ModError(modId, $"展示引用未知 Buff（BuffTypeId）'{display.BuffTypeId}'"));
        registry.RegisterBuff(display);
        declared.Buffs.Add(display.BuffTypeId);
    }

    /// <inheritdoc/>
    public void RegisterUnit(UnitDisplay display) {
        if (content.GetUnit(new UnitConfigKey(display.ConfigKey)) is null)
            errors.Add(new ModError(modId, $"展示引用未知单位 '{display.ConfigKey}'"));
        registry.RegisterUnit(display);
        declared.Units.Add(display.ConfigKey);
    }

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonDisplay display) {
        if (content.GetDungeon(display.Key) is null)
            errors.Add(new ModError(modId, $"展示引用未知副本 '{display.Key}'"));
        registry.RegisterDungeon(display);
        declared.Dungeons.Add(display.Key);
    }
}
