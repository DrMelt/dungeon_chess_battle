namespace DungeonChessBattle.GameConfig.Data;

public class SkillAddBuffConfig : SkillConfig {
    public string BuffId { get; set; } = "";
    public required BuffConfig BuffConfig {
        get; set;
    }
}
