using DungeonChessBattle.Core;
using Godot;

namespace DungeonChessBattle;

public partial class StateBarMark : Control {
    [Export]
    UnitsInScene_Show unitsInScene_Show_Ref;
    [Export]
    PackedScene stateBarSimple2d_PKD;
    public override void _Process(double delta) {
        var children = GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }


        Godot.Collections.Array<UnitState> units = unitsInScene_Show_Ref.UnitsArr;
        foreach (UnitState unit in units) {
            var stateBarSimple2d = stateBarSimple2d_PKD.Instantiate<StateBarMark2d>();
            AddChild(stateBarSimple2d);
            stateBarSimple2d.UpdateUI_WithUnit(unit);
        }


    }
}
