using Godot;

namespace DungeonChessBattle;

public partial class UnitShowArea3D : Area3D {
    [Export]
    UnitGameShow unitShowRef = null!;
    public UnitGameShow UnitShowRef => unitShowRef;
}
