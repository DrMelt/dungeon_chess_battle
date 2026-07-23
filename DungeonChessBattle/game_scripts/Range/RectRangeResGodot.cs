using DungeonChessBattle.Core.Interfaces;
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

    public override bool IsInRange(IUnitState callSkillObject, IUnitState testObject, System.Numerics.Vector3 targetPos) {
        return Utility.IsInRange_Rect(
            testObject.Position,
            callSkillObject.Position,
            targetPos - callSkillObject.Position,
            nearClamp,
            farClamp,
            fromL,
            toR
        );
    }
}
