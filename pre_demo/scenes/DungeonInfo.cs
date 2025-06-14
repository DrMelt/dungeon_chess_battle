using Godot;
using System;

public partial class DungeonInfo : Node
{
    float dungeonTime = 0;
    public float DungeonTime => dungeonTime;


    public override void _PhysicsProcess(double delta)
    {
        dungeonTime += (float)delta;
    }

}
