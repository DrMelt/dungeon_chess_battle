using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.GameConfig.Models;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 旧式静态配置门面：全部转发到 <see cref="GameContentHost.Registry"/>。
/// 保留旧访问形态是为让 Godot 资源脚本与既有消费方零改动迁移；新增代码一律经注册表读取。
/// </summary>
public static class GameConfigDB {
    /// <summary>魔法持续伤害 Buff 定义。</summary>
    public static DamageOverTimeBuff BuffDotMagic =>
        (DamageOverTimeBuff)Registry.GetBuff(1)!;

    /// <summary>物理持续伤害 Buff 定义。</summary>
    public static DamageOverTimeBuff BuffDotPhysical =>
        (DamageOverTimeBuff)Registry.GetBuff(2)!;

    /// <summary>持续治疗 Buff 定义。</summary>
    public static HealOverTimeBuff BuffHot =>
        (HealOverTimeBuff)Registry.GetBuff(3)!;

    /// <summary>魔法单体伤害技能定义。</summary>
    public static DamageSkillDefinition SkillMagicDamage =>
        (DamageSkillDefinition)Registry.GetSkill(new SkillKeyId("skill_magic_damage"))!;

    /// <summary>治疗技能定义。</summary>
    public static HealSkillDefinition SkillCure =>
        (HealSkillDefinition)Registry.GetSkill(new SkillKeyId("skill_cure"))!;

    /// <summary>添加魔法持续伤害 Buff 的技能定义。</summary>
    public static AddBuffSkillDefinition SkillAddDotMagic =>
        (AddBuffSkillDefinition)Registry.GetSkill(new SkillKeyId("skill_add_dot_magic"))!;

    /// <summary>添加持续治疗 Buff 的技能定义。</summary>
    public static AddBuffSkillDefinition SkillAddHot =>
        (AddBuffSkillDefinition)Registry.GetSkill(new SkillKeyId("skill_add_hot"))!;

    /// <summary>矩形范围物理伤害技能定义。</summary>
    public static RangeDamageSkillDefinition SkillRectRangeDamage =>
        (RangeDamageSkillDefinition)Registry.GetSkill(new SkillKeyId("skill_rect_range_damage"))!;

    /// <summary>单体嘲讽仇恨技能定义。</summary>
    public static HateSkillDefinition SkillTaunt =>
        (HateSkillDefinition)Registry.GetSkill(new SkillKeyId("skill_taunt"))!;

    /// <summary>白法师单位配置。</summary>
    public static UnitConfig UnitWhiteMage => Registry.GetUnit("WhiteMage")!;

    /// <summary>哥布林敌人单位配置。</summary>
    public static UnitConfig UnitGoblin => Registry.GetUnit("Goblin")!;

    /// <summary>哥布林首领敌人单位配置。</summary>
    public static UnitConfig UnitGoblinBoss => Registry.GetUnit("GoblinBoss")!;

    /// <summary>默认副本键，创建房间未指定副本时使用；读合并后内容，mod 可覆盖。消费方一律经 <see cref="IDungeonRegistry.DefaultDungeonKey"/> 取用。</summary>
    public static string DefaultDungeonKey =>
        Registry.Content.DefaultDungeonKey ?? BuiltInContent.DefaultDungeonKey;

    /// <summary>副本一：哥布林营地。</summary>
    public static DungeonConfig DungeonGoblinCamp => Registry.GetDungeon("goblin_camp")!;

    /// <summary>副本二：深邃洞窟。</summary>
    public static DungeonConfig DungeonDeepCave => Registry.GetDungeon("deep_cave")!;

    /// <summary>战斗数据集修订号，随 mod 内容指纹联动；内容侧任何变化都会改变值。</summary>
    public static string DataRevision => Registry.DataRevision;

    private static ContentSetRegistry Registry => GameContentHost.Registry;

    /// <summary>按技能强类型 ID 查找技能定义；未找到返回 null。</summary>
    public static SkillDefinition? GetSkillById(SkillKeyId skillKey) => Registry.GetSkill(skillKey);
}
