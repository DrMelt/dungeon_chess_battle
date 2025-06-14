using Godot;
using System;

public partial class LabelDungeonTime : Label
{
    [Export]
    DungeonInfo DungeonInfoRef;

    public override void _Process(double delta)
    {
        Text = "Time: " + DungeonInfoRef.DungeonTime.ToString("F0");
    }

}
