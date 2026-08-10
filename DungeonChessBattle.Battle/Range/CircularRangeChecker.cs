using System.Numerics;

namespace DungeonChessBattle.Battle.Range;

/// <summary>
/// 扇形（环形）范围判定器：以施法者为圆心、正对目标位置方向，判定目标是否处于角度与半径范围内。
/// </summary>
public class CircularRangeChecker : IRangeChecker {
    /// <summary>近端半径。</summary>
    public float NearClamp {
        get; set;
    }

    /// <summary>远端半径。</summary>
    public float FarClamp {
        get; set;
    }

    /// <summary>扇形起始角（弧度，以朝向为 0）。</summary>
    public float RadianFrom { get; set; } = -MathF.PI;

    /// <summary>扇形结束角（弧度，以朝向为 0）。</summary>
    public float RadianTo { get; set; } = MathF.PI;

    /// <summary>
    /// 判断目标单位是否处于扇形范围内。
    /// </summary>
    /// <param name="callSkillObject">施法单位。</param>
    /// <param name="testObject">被检测的目标单位。</param>
    /// <param name="targetPos">技能指向的目标位置（用于确定扇形朝向）。</param>
    /// <returns>目标处于扇形范围内返回 true。</returns>
    public bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos) {
        return Utility.IsInRange_Circular(
            testObject.Position,
            callSkillObject.Position,
            targetPos - callSkillObject.Position,
            NearClamp,
            FarClamp,
            RadianFrom,
            RadianTo,
            bodyRadius: testObject.BodyRadius);
    }
}
