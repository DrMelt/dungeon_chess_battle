using DungeonChessBattle.Game.GamePlayUI;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 焦点单位轮廓高亮节点，跟随鼠标悬停的单位更新轮廓网格。
/// </summary>
public partial class FocusOnOutline : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<FocusOnOutline> _logger = ServiceLocator.GetLogger<FocusOnOutline>();

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
            _logger.LogError("playerInterfaceRes is not assigned!");
        if (outLineMeshInstance == null)
            _logger.LogError("outLineMeshInstance is not assigned!");
    }

    /// <summary>
    /// 每帧更新轮廓：跟随鼠标悬停的单位位置与网格，无悬停时清除轮廓。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var focused = playerInterfaceRes?.MouseOnUnit;
        var focusedMesh = focused?.IsInsideTree() == true
            ? focused.UnitMeshInstanceRef
            : null;

        if (focused != null && focusedMesh != null && outLineMeshInstance != null) {
            outLineMeshInstance.GlobalTransform = focusedMesh.GlobalTransform;
            outLineMeshInstance.Mesh = focusedMesh.Mesh;
        }
        else {
            outLineMeshInstance?.Mesh = null;
        }
    }
}
