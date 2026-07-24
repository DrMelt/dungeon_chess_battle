using System.Numerics;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Math;

namespace DungeonChessBattle.Core.Range;

public class RectRangeRes : IRangeRes {
    public float NearClamp {
        get; set;
    }
    public float FarClamp {
        get; set;
    }
    public float FromL { get; set; } = -1.0f;
    public float ToR { get; set; } = 1.0f;

    public bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos) {
        return Utility.IsInRange_Rect(
            testObject.Position,
            callSkillObject.Position,
            targetPos - callSkillObject.Position,
            NearClamp,
            FarClamp,
            FromL,
            ToR);
    }
}
