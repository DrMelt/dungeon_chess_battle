using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Mod.Shared;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 引导上下文实现：内容注册转发 <see cref="ContentSetRegistry"/>。
/// mod 数据代码入口经本上下文把领域定义对象写进注册表，内容全部来自 mod。
/// 本类只做转发，写入规则一律落在注册表，mod 专属守卫不设。
/// </summary>
public sealed class ModBootstrapContext(ContentSetRegistry registry) : IModBootstrapContext {
    /// <inheritdoc/>
    public void RegisterSkill(SkillDefinition skill) => registry.RegisterSkill(skill);

    /// <inheritdoc/>
    public void RegisterBuff(BuffDefinition buff) => registry.RegisterBuff(buff);

    /// <inheritdoc/>
    public void RegisterUnit(UnitConfig unit) => registry.RegisterUnit(unit);

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonConfig dungeon) => registry.RegisterDungeon(dungeon);
}
