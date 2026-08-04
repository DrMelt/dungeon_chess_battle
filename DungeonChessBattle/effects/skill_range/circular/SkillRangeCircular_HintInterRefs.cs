using Godot;

namespace DungeonChessBattle;

public partial class SkillRangeCircular_HintInterRefs : Node {
    [Export]
    public MeshInstance3D? DecalRef {
        get; private set;
    }

    public override void _Ready() {
        if (DecalRef == null) {
            GD.PrintErr("[SkillRangeCircular_HintInterRefs] [Export] DecalRef is not assigned!");
        }
    }
}
