using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 焦点单位轮廓高亮节点，跟随鼠标悬停的单位更新轮廓网格。
/// </summary>
public partial class FocusOnOutline : Node {
    /// <summary>玩家界面资源引用，用于获取鼠标悬停的单位。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>轮廓网格实例，用于显示悬停单位的轮廓。</summary>
    [Export]
    private MeshInstance3D? outLineMeshInstance;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (playerInterfaceRes == null)
            GD.PrintErr("[FocusOnOutline] [Export] playerInterfaceRes is not assigned!");
        if (outLineMeshInstance == null)
            GD.PrintErr("[FocusOnOutline] [Export] outLineMeshInstance is not assigned!");
    }

    /// <summary>
    /// 每帧更新轮廓：跟随鼠标悬停的单位位置与网格，无悬停时清除轮廓。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        // Update OutLine
        if (playerInterfaceRes?.MouseOnUnit != null && outLineMeshInstance != null) {
            outLineMeshInstance.GlobalTransform = playerInterfaceRes.MouseOnUnit.UnitMeshInstanceRef.GlobalTransform;
            outLineMeshInstance.Mesh = playerInterfaceRes.MouseOnUnit.UnitMeshInstanceRef.Mesh;
        }
        else {
            outLineMeshInstance?.Mesh = null;
        }
    }

}
