using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

/// <summary>
/// 单位 3D 展示组件。
/// 简化版：移除 NavigationAgent 移动逻辑，位置由 BattlePanel 通过 LES Entity 同步更新。
/// </summary>
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

    [Export]
    UnitShowArea3D? unitShowAreaRef;
    public UnitShowArea3D? UnitShowAreaRef => unitShowAreaRef;

    public void SetUnitGlobalPosition(Vector3 globalPos) {
        unitStateRec.SetGlobalPosition(globalPos);
    }

    public void SetUnitGlobalDir(Vector3 globalDir) {
        unitStateRec.SetLookAt_Dir(globalDir);
    }

    override public void _Process(double delta) {
        GlobalPosition = unitStateRec.Position;
        LookAt(unitStateRec.LookAt_Dir + unitStateRec.Position);
    }
}
