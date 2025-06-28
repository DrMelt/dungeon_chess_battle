using Godot;
using System;

public partial class DungeonEnv : Node3D {

    [ExportGroup("References")]
    [Export]
    StartPointArea startPointAreaRef;
    public StartPointArea StartPointAreaRef => startPointAreaRef;

}
