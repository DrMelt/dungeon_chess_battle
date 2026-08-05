namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 施加 Buff 的技能配置。
/// </summary>
public class SkillAddBuffConfig : SkillConfig {
    /// <summary>释放时施加的 Buff 配置。</summary>
    public required BuffConfig BuffConfig {
        get; set;
    }
}
