using Godot;

namespace DungeonChessBattle;

public partial class EffectHints : Node {
    [Export]
    UserInterfaceRes? userInterfaceRes;

    [Export]
    private PackedScene? _effectCircleRange_PKS;

    [Export]
    private PackedScene? _effectRectRange_PKS;

    public override void _Ready() {
        if (userInterfaceRes == null)
            GD.PrintErr("[EffectHints] [Export] userInterfaceRes is not assigned!");
        if (_effectCircleRange_PKS == null)
            GD.PrintErr("[EffectHints] [Export] _effectCircleRange_PKS is not assigned!");
        if (_effectRectRange_PKS == null)
            GD.PrintErr("[EffectHints] [Export] _effectRectRange_PKS is not assigned!");
    }

    public override void _Process(double delta) {
        var children = GetChildren();
        foreach (Node child in children) {
            if (child is Node3D) {
                // effect cleanup handled by child scripts
            }
        }
    }
}
