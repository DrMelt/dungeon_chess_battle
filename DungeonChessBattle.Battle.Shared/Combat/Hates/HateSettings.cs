namespace DungeonChessBattle.Battle.Shared.Combat.Hates;

/// <summary>
/// 仇恨系统的可调参数，房间装配时可选覆盖。
/// </summary>
public sealed record HateSettings {
    /// <summary>治疗仇恨倍率：治疗量 × 治疗来源仇恨倍率 HateFactor × 此系数。</summary>
    public float HealHateFactor { get; init; } = 1.0f;

    /// <summary>伤害仇恨倍率：伤害量 × 来源仇恨倍率 HateFactor × 此系数。</summary>
    public float DamageHateFactor { get; init; } = 1.0f;

}
