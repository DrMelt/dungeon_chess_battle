using Godot;
using Godot.Collections;
using System;

public partial class TestInit : Node
{
    [Export]
    UnitsInGame unitsInGameRef;

    Random random = new Random();
    public override void _Ready()
    {
        var unitsStateList = unitsInGameRef.GetUnitsStateList();
        foreach (var unitState in unitsStateList)
        {
            unitState.SetGlobalPosition(
                new Vector3(random.Next(-10, 10), 0, random.Next(-10, 10)));
        }
    }

}
