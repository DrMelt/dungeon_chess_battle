using System;
using DungeonChessBattle.ui_units.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class HP_StateBar : Control, IUI_Update {
    public HP_StateBarInterRefs? InterRefs {
        get; private set;
    }
    ShaderMaterial? stateBarMat;

    public override void _Ready() {
        InterRefs = GetNode<HP_StateBarInterRefs>("HP_StateBarInterRefs");
        if (InterRefs?.StateBarRef?.Material is ShaderMaterial mat) {
            stateBarMat = mat;
        }
    }

    public void UpdateUI_WithUnit(UnitState unitState) {
        if (unitState == null || InterRefs == null) {
            return;
        }

        if (stateBarMat != null) {
            Color? campColor = InterRefs.UserUISettingsRef?.GetCampColor(unitState.Camp);
            if (campColor != null) {
                stateBarMat.SetShaderParameter("ParPin_01_Color", (Color)campColor);
            }
            stateBarMat.SetShaderParameter("ParPin_01", unitState.Health_Percent);
        }

        InterRefs.LabelPercentRef!.Text = unitState.Health_Shield_Percent.ToString("P1");
        InterRefs.LabelCurrentValueRef!.Text = unitState.Health_Shield.ToString("F1");
        InterRefs.LabelObjectNameRef!.Text = unitState.UnitStateName;
    }

}
