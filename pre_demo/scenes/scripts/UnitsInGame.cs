using GameLogic;
using Godot;
using System;
using System.Collections.Generic;

public partial class UnitsInGame : Node
{
    public List<UnitState> GetUnitsStateList()
    {
        List<UnitState> unitsStateList = new List<UnitState>();
        var children = GetChildren();
        foreach (var child in children)
        {
            if (child is UnitGameShow unitShow)
            {
                unitsStateList.Add(unitShow.UnitStateRec);
            }
        }
        return unitsStateList;
    }

    public override void _PhysicsProcess(double delta)
    {
        var unitState = GetUnitsStateList();
        foreach (var unit in unitState)
        {
            unit.UpdateBuffList(delta);
        }
    }

}
