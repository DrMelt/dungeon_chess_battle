using System;
using Godot;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.Shared;

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
    /// 按单位展示视图落地外观：视图声明模型场景时隐藏内置默认网格并实例化模型挂入本节点，
    /// 声明配色时对生效网格统一覆写材质；未声明任一字段即保持内置模板原样。
    /// 纯客户端展示数据，不参与内容指纹与结算；须在挂入场景树后调用（AddChild 已触发 _Ready）。
    /// </summary>
    /// <param name="view">单位展示视图，未注册时为 null。</param>
    public void ApplyUnitDisplay(IUnitView? view) {
        if (view == null)
            return;

        Node3D? modelRoot = null;
        if (view.ModelScene is { } modelScene) {
            if (_interRefs?.UnitMeshInstanceRef is { } defaultMesh)
                defaultMesh.Visible = false;
            modelRoot = modelScene.Instantiate<Node3D>();
            AddChild(modelRoot);
        }

        if (view.BodyColor is { } bodyColor) {
            Node? target = modelRoot ?? _interRefs?.UnitMeshInstanceRef;
            if (target is not null)
                ApplyBodyColor(target, bodyColor);
        }
    }

    /// <summary>对节点子树内全部网格统一覆写配色材质，仅影响外观。</summary>
    private static void ApplyBodyColor(Node root, Color color) {
        var material = new StandardMaterial3D { AlbedoColor = color };
        ApplyMaterial(root);

        void ApplyMaterial(Node node) {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = material;
            foreach (var child in node.GetChildren())
                ApplyMaterial(child);
        }
    }

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
