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

    public override bool IsInRange(IUnitState callSkillObject, IUnitState testObject, System.Numerics.Vector3 targetPos) {
        return Utility.IsInRange_Circular(
            testObject.Position,
            callSkillObject.Position,
            targetPos - callSkillObject.Position,
            nearClamp,
            farClamp,
            radianFrom,
            radianTo
        );
    }
}
