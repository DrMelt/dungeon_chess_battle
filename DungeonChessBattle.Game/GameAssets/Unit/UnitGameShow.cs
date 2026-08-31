using System;
using Godot;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 单位 3D 展示组件。
/// 绑定本地展示视图（<see cref="IUnitUiView"/>），每帧直读位置/朝向驱动网格。
/// </summary>
public partial class UnitGameShow : Node3D {
    /// <summary>本地展示视图。</summary>
    public IUnitUiView Unit {
        get => field ?? throw new InvalidOperationException("Unit has not been assigned.");
        set;
    }

    /// <summary>导出引用集合节点。</summary>
    private UnitGameShowInterRefs? _interRefs;

    /// <summary>单位网格实例。</summary>
    public MeshInstance3D? UnitMeshInstanceRef => _interRefs?.UnitMeshInstanceRef;

    /// <summary>单位点击交互区域。</summary>
    public UnitShowArea3D? UnitShowAreaRef => _interRefs?.UnitShowAreaRef;

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        _interRefs = GetNode<UnitGameShowInterRefs>("UnitGameShowInterRefs");
    }

    /// <summary>
    /// 每帧从本地展示视图直读位置与朝向（XZ 平面）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var pos = Unit.Position;
        GlobalPosition = new Vector3(pos.X, 0f, pos.Y);

        var dir = Unit.Direction;
        if (dir.LengthSquared() > 0.0001f) {
            LookAt(GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
        }
    }
}
