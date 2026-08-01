using Godot;

namespace DungeonChessBattle;

public partial class DungeonEnv : Node3D {
    [ExportGroup("References")]
    [Export]
    StartPointArea startPointAreaRef = null!;
    public StartPointArea StartPointAreaRef => startPointAreaRef;
}
