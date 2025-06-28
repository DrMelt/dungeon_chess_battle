using GameLogic;
using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class UnitsInScene_Show : Node {
    UnitsInScene unitsInSceneRes = new UnitsInScene();

    bool isPause = false;


    public UnitsInScene UnitsInSceneRes => unitsInSceneRes;

    public Array<UnitState> UnitsArr => unitsInSceneRes.UnitsArr;


    public override void _Process(double delta) {
        if (Input.IsActionJustPressed("Scene_Pause")) {
            isPause = !isPause;
        }
    }
    public override void _PhysicsProcess(double delta) {
        if (!isPause) {
            unitsInSceneRes.UpdateState(delta);
        }
    }


    public void AddUnitShow(UnitGameShow unitGameShow) {
        unitsInSceneRes.AddUnit(unitGameShow.UnitStateRec);
        AddChild(unitGameShow);
    }
}
