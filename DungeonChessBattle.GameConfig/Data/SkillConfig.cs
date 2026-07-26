namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 技能配置基类，仅包含策划配表参数，不含运行时状态
/// </summary>
public class SkillConfig {
    public float SkillSpellTime { get; set; } = 2.0f;
    public float SkillCooldownTime { get; set; } = 3.0f;
    public float GCDTime { get; set; } = 3.0f;
    public bool NeedUnitTarget {
        get; set;
    }
    public bool NeedPosTarget {
        get; set;
    }
    public string SkillCanAdd { get; set; } = "None";
}
