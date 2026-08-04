using System;
using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

/// <summary>
/// 单位 3D 展示组件。
/// 简化版：移除 NavigationAgent 移动逻辑，位置由 BattlePanel 通过 LES Entity 同步更新。
/// </summary>
public partial class UnitGameShow : Node3D {
    [Export]
    UnitState? unitStateRec;
    public UnitState UnitStateRec {
        get => unitStateRec ?? throw new InvalidOperationException("UnitStateRec has not been assigned.");
        set => unitStateRec = value;
    }

    public Array<UnitSkillBaseGodot> SkillsList => UnitStateRec.SkillsList;

    [Export]
    MeshInstance3D? unitMeshInstanceRef;
    public MeshInstance3D UnitMeshInstanceRef => unitMeshInstanceRef ?? throw new InvalidOperationException("UnitMeshInstanceRef has not been assigned.");

    [Export]
    UnitShowArea3D? unitShowAreaRef;
    public UnitShowArea3D? UnitShowAreaRef => unitShowAreaRef;

    public override void _Ready() {
        if (unitStateRec == null)
            GD.PrintErr("[UnitGameShow] [Export] unitStateRec is not assigned!");
        if (unitMeshInstanceRef == null)
            GD.PrintErr("[UnitGameShow] [Export] unitMeshInstanceRef is not assigned!");
    }

    public void SetUnitGlobalPosition(Vector3 globalPos) {
        if (unitStateRec == null)
            return;
        unitStateRec.SetGlobalPosition(globalPos);
    }

    public void SetUnitGlobalDir(Vector3 globalDir) {
        if (unitStateRec == null)
            return;
        unitStateRec.SetLookAt_Dir(globalDir);
    }

    override public void _Process(double delta) {
        if (unitStateRec == null)
            return;
        GlobalPosition = unitStateRec.Position;
        LookAt(unitStateRec.LookAt_Dir + unitStateRec.Position);
    }
}
