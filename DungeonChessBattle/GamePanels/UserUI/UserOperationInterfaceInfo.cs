using Godot;

namespace DungeonChessBattle;

public partial class UserOperationInterfaceInfo : Node {
    [Export]
    Camera3D camera3DRef = null!;

    [Export]
    Node2d_UserUI node2dUiRef = null!;

    [Export]
    UserInterfaceRes userInterfaceRes = null!;

    [ExportGroup("Intrinsic Parameter")]
    [Export]
    EffectHints effectHintsRef = null!;
}
