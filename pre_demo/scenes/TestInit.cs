using Godot;
using Godot.Collections;
using System;

public partial class TestInit : Node {
    [Export]
    UnitsInScene_Show unitsInScene_Show;
    [Export]
    StartPointArea startPointAreaRef;

    Random random = new Random();
    public override void _Ready() {
        DevelopmentInit();

        var unitsStateList = unitsInScene_Show.UnitsArr;
        foreach (var unitState in unitsStateList) {
            unitState.SetGlobalPosition(startPointAreaRef.SamplePosition());
        }

    }
    void DevelopmentInit() {
        var children = unitsInScene_Show.GetChildren();
        foreach (Node child in children) {
            if (child is UnitGameShow unitGameShow) {
                unitsInScene_Show.UnitsInSceneRes.AddUnit(unitGameShow.UnitStateRec);
            }
        }
    }
}
