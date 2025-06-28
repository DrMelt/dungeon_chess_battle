using GameLogic;
using Godot;
using System;

public partial class StateBarMark2d : Control, I_UI_Update {
    [Export]
    HP_StateBar panelUnitStateBarRef;

    public void UpdateUI_WithUnit(UnitState unitState) {
        var camera3D = GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(unitState.Position + Vector3.Up * 2.2f);
        GlobalPosition = screenPos;

        panelUnitStateBarRef.UpdateUI_WithUnit(unitState);
    }
}
