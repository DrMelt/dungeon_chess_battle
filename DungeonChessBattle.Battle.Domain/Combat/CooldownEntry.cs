namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>个体冷却项：技能键与剩余秒数，服务端权威状态，原地推进避免每帧分配。</summary>
public sealed class CooldownEntry(SkillKeyId skillKey, float remaining) {
    /// <summary>技能键。</summary>
    public SkillKeyId SkillKey = skillKey;

    /// <summary>剩余冷却秒数。</summary>
    public float Remaining = remaining;
}
