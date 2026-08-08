using System;
using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

/// <summary>
/// 单位 3D 展示组件。
/// 简化版：移除 NavigationAgent 移动逻辑，位置由 BattlePanel 通过 LES Entity 同步更新。
/// </summary>
public partial class UnitGameShow : Node3D {
    /// <summary>单位状态资源（运行时注入，由 MainScene.SpawnUnit 赋值，非场景导出）。</summary>
    private UnitState? _unitStateRec;

    /// <summary>单位状态资源。</summary>
    public UnitState UnitStateRec {
        get => _unitStateRec ?? throw new InvalidOperationException("UnitStateRec has not been assigned.");
        set => _unitStateRec = value;
    }

    /// <summary>单位技能列表。</summary>
    public Array<UnitSkillBaseGodot> SkillsList => UnitStateRec.SkillsList;

    /// <summary>导出引用集合节点。</summary>
    public UnitGameShowInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>单位网格实例。</summary>
    public MeshInstance3D? UnitMeshInstanceRef => InterRefs?.UnitMeshInstanceRef;

    /// <summary>单位点击交互区域。</summary>
    public UnitShowArea3D? UnitShowAreaRef => InterRefs?.UnitShowAreaRef;

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<UnitGameShowInterRefs>("UnitGameShowInterRefs");
    }

    /// <summary>
    /// 设置单位在世界坐标中的位置。
    /// </summary>
    /// <param name="globalPos">世界坐标位置。</param>
    public void SetUnitGlobalPosition(Vector3 globalPos) {
        UnitStateRec.SetGlobalPosition(globalPos);
    }

    /// <summary>
    /// 设置单位朝向方向。
    /// </summary>
    /// <param name="globalDir">世界朝向方向。</param>
    public void SetUnitGlobalDir(Vector3 globalDir) {
        UnitStateRec.SetLookAt_Dir(globalDir);
    }


    /// <summary>
    /// 每帧从单位状态同步位置与朝向。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    override public void _Process(double delta) {
        GlobalPosition = UnitStateRec.Position;

        LookAt(UnitStateRec.LookAt_Dir + UnitStateRec.Position);
    }
}
