namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 治疗技能配置。
/// </summary>
public class SkillCureConfig : SkillConfig {
    /// <summary>治疗量基础值，经施法单位治疗强度换算。</summary>
    public float CurePotency {
        get; set;
    }
}
