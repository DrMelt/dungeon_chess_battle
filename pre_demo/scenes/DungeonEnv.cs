using Godot;

namespace DungeonChessBattle;

public partial class DungeonEnv : Node3D {

    [ExportGroup("References")]
    [Export]
    StartPointArea startPointAreaRef;
    public StartPointArea StartPointAreaRef => startPointAreaRef;

}
