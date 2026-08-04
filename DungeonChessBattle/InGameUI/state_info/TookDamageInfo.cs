using Godot;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle;

public partial class TookDamageInfo : FadeInfo {


    [ExportGroup("Internal")]
    [Export]
    Label? damageLabel;

    public override void _Ready() {
        base._Ready();
        if (damageLabel == null)
            GD.PrintErr("[TookDamageInfo] [Export] damageLabel is not assigned!");
    }

    public void Init(float damage, Enum_DamageType type, UserUISettings userUISettings) {
        if (damageLabel == null)
            return;
        if (type == Enum_DamageType.Magic) {
            damageLabel.SelfModulate = userUISettings.MagicInfoColor;
        }
        else if (type == Enum_DamageType.Physcial) {
            damageLabel.SelfModulate = userUISettings.PhysicalInfoColor;
        }
        damageLabel.Text = damage.ToString("F0");
    }

    public override void _Process(double delta) {
        UpdateFade(delta);
    }
}
