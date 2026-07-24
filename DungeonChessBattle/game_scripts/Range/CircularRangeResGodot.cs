using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Range;
using Godot;

namespace DungeonChessBattle.Core.Range;

[GlobalClass]
public partial class CircularRangeResGodot : RangeResBaseGodot {
    [Export]
    float nearClamp = 0.0f;
    public float NearClamp => nearClamp;

    [Export]
    float farClamp = 1.0f;
    public float FarClamp => farClamp;

    [Export]
    float radianFrom = -1.0f;
    public float RadianFrom => radianFrom;

    [Export]
    float radianTo = 1.0f;
    public float RadianTo => radianTo;

    protected override IRangeRes CreateModel() {
        return new CircularRangeRes {
            NearClamp = nearClamp,
            FarClamp = farClamp,
            RadianFrom = radianFrom,
            RadianTo = radianTo,
        };
    }
}
