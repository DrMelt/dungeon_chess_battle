using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 技能效果提示管理器，持有圆形/矩形范围提示场景资源。
/// </summary>
public partial class EffectHints : Node {
    /// <summary>玩家界面资源引用。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>圆形范围提示使用的场景资源。</summary>
    [Export]
    private PackedScene? _effectCircleRange_PKS;

    /// <summary>矩形范围提示使用的场景资源。</summary>
    [Export]
    private PackedScene? _effectRectRange_PKS;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (playerInterfaceRes == null)
            GD.PrintErr("[EffectHints] [Export] playerInterfaceRes is not assigned!");
        if (_effectCircleRange_PKS == null)
            GD.PrintErr("[EffectHints] [Export] _effectCircleRange_PKS is not assigned!");
        if (_effectRectRange_PKS == null)
            GD.PrintErr("[EffectHints] [Export] _effectRectRange_PKS is not assigned!");
    }

    /// <summary>
    /// 每帧遍历子效果节点，清理由子脚本自行处理。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var children = GetChildren();
        foreach (Node child in children) {
            if (child is Node3D) {
                // effect cleanup handled by child scripts
            }
        }
    }
}
