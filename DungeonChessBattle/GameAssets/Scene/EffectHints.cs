using Godot;

namespace DungeonChessBattle;

public partial class EffectHints : Node {
    [Export]
    UserInterfaceRes userInterfaceRes = null!;

    [Export]
    UnitsInScene_Show unitsInScene_Show_Ref = null!;

    [Export]
    Node2d_UserUI userUI_Ref = null!;

    [Export]
    private PackedScene? _effectCircleRange_PKS;

    [Export]
    private PackedScene? _effectRectRange_PKS;

    public override void _Process(double delta) {
        var children = GetChildren();
        foreach (Node child in children) {
            if (child is Node3D) {
                // effect cleanup handled by child scripts
            }
        }
    }
}
