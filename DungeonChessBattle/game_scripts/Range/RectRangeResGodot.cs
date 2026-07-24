using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Range;
using Godot;

namespace DungeonChessBattle.Core.Range;

[GlobalClass]
public partial class RectRangeResGodot : RangeResBaseGodot {
    [Export]
    float nearClamp = 0.0f;
    public float NearClamp => nearClamp;

    [Export]
    float farClamp = 1.0f;
    public float FarClamp => farClamp;

    [Export]
    float fromL = -1.0f;
    public float FromL => fromL;

    [Export]
    float toR = 1.0f;
    public float ToR => toR;

    protected override IRangeRes CreateModel() {
        return new RectRangeRes {
            NearClamp = nearClamp,
            FarClamp = farClamp,
            FromL = fromL,
            ToR = toR,
        };
    }
}
