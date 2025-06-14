using Godot;
using System;

public partial class StartPointArea : Node3D
{
    [Export]
    float startPointRadius = 2.0f;

    Random random = new Random();

    public Vector3 SamplePosition()
    {
        float angle = random.NextSingle() * 2 * Mathf.Pi;
        float radius = startPointRadius * Mathf.Sqrt(random.NextSingle()); 

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        return new Vector3(x, 0, z);
    }

}
