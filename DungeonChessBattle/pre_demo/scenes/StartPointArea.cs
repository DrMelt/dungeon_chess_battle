using Godot;
using System;

namespace DungeonChessBattle;

public partial class StartPointArea : Node3D {
    [Export]
    float startPointRadius = 2.0f;

    readonly Random random = new();

    public Vector3 SamplePosition() {
        float angle = random.NextSingle() * 2 * Mathf.Pi;
        float radius = startPointRadius * Mathf.Sqrt(random.NextSingle());

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        return new Vector3(x, 0, z) + GlobalPosition;
    }

}
