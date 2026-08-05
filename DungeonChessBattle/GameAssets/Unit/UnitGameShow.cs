using System;
using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

/// <summary>
/// 单位 3D 展示组件。
/// 简化版：移除 NavigationAgent 移动逻辑，位置由 BattlePanel 通过 LES Entity 同步更新。
/// </summary>
public partial class UnitGameShow : Node3D {
    /// <summary>单位状态资源引用。</summary>
    [Export]
    private UnitState? unitStateRec;
    /// <summary>单位状态资源。</summary>
    public UnitState UnitStateRec {
        get => unitStateRec ?? throw new InvalidOperationException("UnitStateRec has not been assigned.");
        set => unitStateRec = value;
    }

    /// <summary>单位技能列表。</summary>
    public Array<UnitSkillBaseGodot> SkillsList => UnitStateRec.SkillsList;

    /// <summary>单位网格实例引用。</summary>
    [Export]
    private MeshInstance3D? unitMeshInstanceRef;
    /// <summary>单位网格实例。</summary>
    public MeshInstance3D UnitMeshInstanceRef => unitMeshInstanceRef ?? throw new InvalidOperationException("UnitMeshInstanceRef has not been assigned.");

    /// <summary>单位点击交互区域。</summary>
    [field: Export]
    public UnitShowArea3D? UnitShowAreaRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (unitStateRec == null)
            GD.PrintErr("[UnitGameShow] [Export] unitStateRec is not assigned!");
        if (unitMeshInstanceRef == null)
            GD.PrintErr("[UnitGameShow] [Export] unitMeshInstanceRef is not assigned!");
    }

    /// <summary>
    /// 设置单位在世界坐标中的位置。
    /// </summary>
    /// <param name="globalPos">世界坐标位置。</param>
    public void SetUnitGlobalPosition(Vector3 globalPos) {
        if (unitStateRec == null)
            return;
        unitStateRec.SetGlobalPosition(globalPos);
    }

    /// <summary>
    /// 设置单位朝向方向。
    /// </summary>
    /// <param name="globalDir">世界朝向方向。</param>
    public void SetUnitGlobalDir(Vector3 globalDir) {
        if (unitStateRec == null)
            return;
        unitStateRec.SetLookAt_Dir(globalDir);
    }

    /// <summary>
    /// 每帧从单位状态同步位置与朝向。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    override public void _Process(double delta) {
        if (unitStateRec == null)
            return;
        GlobalPosition = unitStateRec.Position;
        LookAt(unitStateRec.LookAt_Dir + unitStateRec.Position);
    }
}
