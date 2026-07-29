using DungeonChessBattle.Core.Enums;
using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

public partial class UnitGameShow : Node3D {
    [Export]
    UnitState unitStateRec = null!;
    public UnitState UnitStateRec {
        get => unitStateRec;
        set => unitStateRec = value;
    }

    public Array<UnitSkillBaseGodot> SkillsList => unitStateRec.SkillsList;

    [Export]
    MeshInstance3D unitMeshInstanceRef = null!;
    public MeshInstance3D UnitMeshInstanceRef => unitMeshInstanceRef;


    [ExportGroup("References")]
    [Export]
    NavigationAgent3dMoveTo navigationAgentRef = null!;

    [Export]
    Node3dTargetMark targetDecalRef = null!;





    public void SetMoveTarget(Vector3 targetPos) {
        if (unitStateRec.Camp == EnumCamp.Camp_A) {
            navigationAgentRef.TargetPos = targetPos;
        }
    }

    public void SetUnitGlobalPosition(Vector3 globalPos) {
        unitStateRec.SetGlobalPosition(globalPos);
    }
    public void SetUnitGlobalDir(Vector3 globalDir) {
        unitStateRec.SetLookAt_Dir(globalDir);
    }


    override public void _Process(double delta) {
        GlobalPosition = unitStateRec.Position;
        LookAt(unitStateRec.LookAt_Dir + unitStateRec.Position);

        targetDecalRef.UpdateUI_WithUnit(unitStateRec);
    }
}
