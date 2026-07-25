namespace DungeonChessBattle.GameConfig.Data;

public class SkillAddBuffConfig : SkillConfig {
    public string BuffId { get; set; } = "";
    public BuffConfig BuffConfig { get; set; } = null!;
}
