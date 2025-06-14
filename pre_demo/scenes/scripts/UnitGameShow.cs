using Godot;
using System;
using Godot.Collections;

using GameLogic;

public partial class UnitGameShow : Node3D
{
    [Export]
    UnitState unitStateRec;
    public UnitState UnitStateRec => unitStateRec;

    [Export]
    Array<UnitSkillBase> skillsList;
    public Array<UnitSkillBase> SkillsList => skillsList;



    [ExportGroup("References")]
    [Export]
    NavigationAgent3D navigationAgentRef;

    [Export]
    DecalTargetMark targetDecalRef;
    public DecalTargetMark TargetDecalRef => targetDecalRef;


    [Export]
    MeshInstance3D unitMeshInstanceRef;
    public MeshInstance3D UnitMeshInstanceRef => unitMeshInstanceRef;

    [Export]
    StateBar stateBarRef;


    override public void _Process(double delta)
    {
        GlobalPosition = unitStateRec.Position;
        LookAt(unitStateRec.LookAt_Dir + unitStateRec.Position);

        stateBarRef.UpdateUI_WithUnit(this);
    }
}
