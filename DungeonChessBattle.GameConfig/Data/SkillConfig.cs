namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 技能配置基类，仅包含策划配表参数，不含运行时状态
/// </summary>
public class SkillConfig {
    /// <summary>技能全局唯一 ID（配置表标识，0 为保留/无效值）。</summary>
    public ushort Id { get; set; } = 0;

    /// <summary>技能读条时间（秒）。</summary>
    public float SkillSpellTime { get; set; } = 2.0f;

    /// <summary>技能自身冷却时间（秒）。</summary>
    public float SkillCooldownTime { get; set; } = 3.0f;

    /// <summary>释放成功后触发的全局冷却时间（秒）。</summary>
    public float GCDTime { get; set; } = 3.0f;

    /// <summary>是否需要锁定单位目标才能释放。</summary>
    public bool NeedUnitTarget {
        get; set;
    }

    /// <summary>是否需要指定位置目标才能释放。</summary>
    public bool NeedPosTarget {
        get; set;
    }

    /// <summary>技能可释放的目标类型标志（SkillCanAdd 枚举名）。</summary>
    public string SkillCanAdd { get; set; } = "None";
}
