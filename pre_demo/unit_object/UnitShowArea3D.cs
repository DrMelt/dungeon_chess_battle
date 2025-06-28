using Godot;
using System;

public partial class UnitShowArea3D : Area3D
{
    [Export]
    UnitGameShow unitShowRef;
    public UnitGameShow UnitShowRef => unitShowRef;
}
