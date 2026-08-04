using DungeonChessBattle.ui_units.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class StateBarMark2d : Control, IUI_Update {
    public StateBarMark2dInterRefs? InterRefs {
        get; private set;
    }

    public override void _Ready() {
        InterRefs = GetNode<StateBarMark2dInterRefs>("StateBarMark2dInterRefs");
    }

    public void UpdateUI_WithUnit(UnitState unitState) {
        var camera3D = GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(unitState.Position + Vector3.Up * 2.2f);
        GlobalPosition = screenPos;

        InterRefs?.PanelUnitStateBarRef?.UpdateUI_WithUnit(unitState);
    }
}
