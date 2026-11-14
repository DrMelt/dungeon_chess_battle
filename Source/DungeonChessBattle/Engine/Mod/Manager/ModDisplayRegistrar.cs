using DungeonChessBattle.Battle.Config.Shared;
using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Game.Display.Registry;
using DungeonChessBattle.Game.Shared.Display;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// mod 条目数据装配面：一个 mod 一个实例，包装 <see cref="DisplayRegistry"/> 递给该 mod 的展示代码入口。
/// 错误按归属 mod 记录，并按只读视图校验展示引用的内容键存在于内容注册表；键不存在记错误，不中断其余注册。
/// </summary>
public sealed class ModDisplayRegistrar(
    DisplayRegistry registry,
    IContentRegistryView content,
    List<ModError> errors,
    string modId) : IDisplayRegistrar {
    /// <inheritdoc/>
    public void RegisterSkill(SkillDisplay display) {
        if (display.SkillId.IsDefault)
            return;
        if (content.GetSkill(display.SkillId) is null)
            errors.Add(new ModError(modId, $"展示引用未知技能 '{display.SkillId.Id}'"));
        registry.RegisterSkill(display);
    }

    /// <inheritdoc/>
    public void RegisterBuff(BuffDisplay display) {
        if (display.BuffTypeId.IsDefault)
            return;
        if (content.GetBuff(display.BuffTypeId) is null)
            errors.Add(new ModError(modId, $"展示引用未知 Buff '{display.BuffTypeId.Value}'"));
        registry.RegisterBuff(display);
    }

    /// <inheritdoc/>
    public void RegisterUnit(UnitDisplay display) {
        if (display.ConfigKey.IsDefault)
            return;
        if (content.GetUnit(display.ConfigKey) is null)
            errors.Add(new ModError(modId, $"展示引用未知单位 '{display.ConfigKey.Value}'"));
        registry.RegisterUnit(display);
    }

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonDisplay display) {
        if (display.DungeonKey.IsDefault)
            return;
        if (content.GetDungeon(display.DungeonKey) is null)
            errors.Add(new ModError(modId, $"展示引用未知副本 '{display.DungeonKey.Value}'"));
        registry.RegisterDungeon(display);
    }
}
