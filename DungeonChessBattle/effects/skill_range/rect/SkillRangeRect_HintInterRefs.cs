using System;
using Godot;

namespace DungeonChessBattle;

public partial class SkillRangeRect_HintInterRefs : Node {
    [Export]
    public MeshInstance3D DecalRef { get; set; } = null!;

    public override void _Ready() {
        if (DecalRef == null) {
            GD.PrintErr("[SkillRangeRect_HintInterRefs] [Export] DecalRef is not assigned!");
        }
    }
}
