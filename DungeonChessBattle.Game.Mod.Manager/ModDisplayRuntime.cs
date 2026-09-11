using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// mod 侧声明过的展示键全集，跨 mod 汇总。宿主据此判定条目是否被 mod 覆盖、
/// 该以已落地的资源为模板改写还是直接补占位。
/// </summary>
public sealed class ModDeclaration {
    /// <summary>被 mod 声明过的技能键。</summary>
    public HashSet<SkillKeyId> Skills { get; } = [];

    /// <summary>被 mod 声明过的 Buff 键。</summary>
    public HashSet<BuffTypeId> Buffs { get; } = [];

    /// <summary>被 mod 声明过的单位配置键。</summary>
    public HashSet<UnitConfigKey> Units { get; } = [];

    /// <summary>被 mod 声明过的副本键。</summary>
    public HashSet<DungeonKeyId> Dungeons { get; } = [];
}

/// <summary>
/// mod 条目数据装配面：一个 mod 一个实例，包装 <see cref="DisplayRegistry"/> 递给该 mod 的展示代码入口。
/// 声明过的展示键记进跨 mod 汇总的 <see cref="ModDeclaration"/>，错误按归属 mod 记录，
/// 并按只读视图校验展示引用的内容键存在于内容注册表——不存在则记错误，不中断其余 mod。
/// 场景资源不经本面：场景名无 mod 归属语义，mod 经装配上下文的注册表口注册与查询。
/// </summary>
public sealed class ModDisplayRuntime(
    DisplayRegistry registry,
    IContentRegistryView content,
    List<ModError> errors,
    string modId,
    ModDeclaration declared) : IModDisplayRuntime {
    /// <inheritdoc/>
    public void RegisterSkill(SkillDisplay display) {
        if (display.SkillId.IsDefault)
            return;
        if (content.GetSkill(display.SkillId) is null)
            errors.Add(new ModError(modId, $"展示引用未知技能 '{display.SkillId.Id}'"));
        registry.RegisterSkill(display);
        declared.Skills.Add(display.SkillId);
    }

    /// <inheritdoc/>
    public void RegisterBuff(BuffDisplay display) {
        if (display.BuffTypeId.IsDefault)
            return;
        if (content.GetBuff(display.BuffTypeId) is null)
            errors.Add(new ModError(modId, $"展示引用未知 Buff '{display.BuffTypeId.Value}'"));
        registry.RegisterBuff(display);
        declared.Buffs.Add(display.BuffTypeId);
    }

    /// <inheritdoc/>
    public void RegisterUnit(UnitDisplay display) {
        if (display.ConfigKey.IsDefault)
            return;
        if (content.GetUnit(display.ConfigKey) is null)
            errors.Add(new ModError(modId, $"展示引用未知单位 '{display.ConfigKey.Value}'"));
        registry.RegisterUnit(display);
        declared.Units.Add(display.ConfigKey);
    }

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonDisplay display) {
        if (display.DungeonKey.IsDefault)
            return;
        if (content.GetDungeon(display.DungeonKey) is null)
            errors.Add(new ModError(modId, $"展示引用未知副本 '{display.DungeonKey.Value}'"));
        registry.RegisterDungeon(display);
        declared.Dungeons.Add(display.DungeonKey);
    }
}
