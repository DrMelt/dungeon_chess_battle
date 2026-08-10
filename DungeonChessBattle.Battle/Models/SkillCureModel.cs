namespace DungeonChessBattle.Battle.Models;

/// <summary>
/// 治疗技能模型：释放时为目标恢复生命值。
/// </summary>
public class SkillCureModel : SkillModel {
    /// <summary>治疗量基础值（经施法单位治疗强度换算）。</summary>
    public float CurePotency {
        get; set;
    }
}
