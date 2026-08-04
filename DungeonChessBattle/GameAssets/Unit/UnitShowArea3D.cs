using Godot;
using System;

namespace DungeonChessBattle;

public partial class UnitShowArea3D : Area3D {
    [Export]
    UnitGameShow? unitShowRef;
    public UnitGameShow UnitShowRef => unitShowRef ?? throw new InvalidOperationException("UnitShowRef has not been assigned.");

    public override void _Ready() {
        if (unitShowRef == null)
            GD.PrintErr("[UnitShowArea3D] [Export] unitShowRef is not assigned!");
    }
}
