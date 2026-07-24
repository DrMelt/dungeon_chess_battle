using System;
using System.Numerics;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Math;

namespace DungeonChessBattle.Core.Range;

public class CircularRangeRes : IRangeRes {
    public float NearClamp {
        get; set;
    }
    public float FarClamp {
        get; set;
    }
    public float RadianFrom { get; set; } = -MathF.PI;
    public float RadianTo { get; set; } = MathF.PI;

    public bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos) {
        return Utility.IsInRange_Circular(
            testObject.Position,
            callSkillObject.Position,
            targetPos - callSkillObject.Position,
            NearClamp,
            FarClamp,
            RadianFrom,
            RadianTo);
    }
}
