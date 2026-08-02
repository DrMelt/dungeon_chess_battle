using Godot;

namespace DungeonChessBattle;

public partial class Node3dTargetMarkInterRefs : Node {
    [Export]
    public UserInterfaceRes UserInterfaceRes { get; set; } = null!;

    [Export]
    public Decal TargetDecalRef { get; set; } = null!;

    [Export]
    public Color DefultColor { get; set; } = new("ad9b24");

    [Export]
    public UserUISettings UserUISettingsRes { get; set; } = null!;

    public override void _Ready() {
        if (UserInterfaceRes == null)
            GD.PrintErr("[Node3dTargetMarkInterRefs] [Export] UserInterfaceRes is not assigned!");
        if (TargetDecalRef == null)
            GD.PrintErr("[Node3dTargetMarkInterRefs] [Export] TargetDecalRef is not assigned!");
        if (UserUISettingsRes == null)
            GD.PrintErr("[Node3dTargetMarkInterRefs] [Export] UserUISettingsRes is not assigned!");
    }
}
