using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class UserUISettings : Resource {
    [ExportGroup("State Info")]
    [Export]
    public Color HealthInfoColor { get; private set; } = new(1, 1, 1, 1);

    [Export]
    public Color PhysicalInfoColor { get; private set; } = new(1, 1, 1, 1);

    [Export]
    public Color MagicInfoColor { get; private set; } = new(1, 1, 1, 1);

    [ExportGroup("Camp Info")]
    [Export]
    public Color AllyCampColor { get; private set; } = new(1, 1, 1, 1);

    [Export]
    public Color NeutralCampColor { get; private set; } = new(1, 1, 1, 1);

    [Export]
    public Color EnemyCampColor { get; private set; } = new(1, 1, 1, 1);

    public Color? GetCampColor(string camp) {
        if (string.IsNullOrEmpty(camp)) return NeutralCampColor;
        return camp switch {
            "Camp_A" or "0" => AllyCampColor,
            "Camp_B" or "2" => EnemyCampColor,
            "Camp_BOSS" or "1" => NeutralCampColor,
            _ => AllyCampColor
        };
    }
}
