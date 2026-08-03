using DungeonChessBattle.ui_units.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class HP_StateBar : Control, IUI_Update {
    public HP_StateBarInterRefs? InterRefs {
        get; private set;
    }

    public override void _Ready() {
        InterRefs = GetNode<HP_StateBarInterRefs>("HP_StateBarInterRefs");
    }

    public void UpdateUI_WithUnit(UnitState unitState) {
        if (unitState == null || InterRefs == null) {
            return;
        }

        var progressBar = InterRefs.ProgressBarRef;
        if (progressBar != null) {
            Color? campColor = InterRefs.UserUISettingsRef?.GetCampColor(unitState.Camp);
            progressBar.SelfModulate = campColor ?? Colors.White;
            progressBar.Value = unitState.Health_Percent;
        }

        InterRefs.LabelPercentRef!.Text = unitState.Health_Shield_Percent.ToString("P1");
        InterRefs.LabelCurrentValueRef!.Text = unitState.Health_Shield.ToString("F1");
        InterRefs.LabelObjectNameRef!.Text = unitState.UnitStateName;
    }

}
