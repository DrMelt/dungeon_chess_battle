namespace DungeonChessBattle.Battle.Shared.Combat.Hates;

/// <summary>
/// 仇恨倍率：伤害与治疗各自相对来源单位仇恨倍率的系数。
/// 由引擎结算侧给出，经 <see cref="IHateRule"/> 上下文交给规则，不由内容配置。
/// </summary>
/// <param name="DamageHateFactor">伤害仇恨倍率：伤害量 × 来源仇恨倍率 HateFactor × 此系数。</param>
/// <param name="HealHateFactor">治疗仇恨倍率：治疗量 × 治疗来源仇恨倍率 HateFactor × 此系数。</param>
public sealed record HateSettings(float DamageHateFactor, float HealHateFactor);
