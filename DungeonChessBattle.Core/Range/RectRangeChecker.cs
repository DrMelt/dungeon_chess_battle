using System.Numerics;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Math;

namespace DungeonChessBattle.Core.Range;

/// <summary>
/// 矩形范围判定器：以施法者为起点、正对目标位置方向，判定目标是否处于矩形范围内。
/// </summary>
public class RectRangeChecker : IRangeChecker {
    /// <summary>近端沿朝向的边界。</summary>
    public float NearClamp {
        get; set;
    }

    /// <summary>远端沿朝向的边界。</summary>
    public float FarClamp {
        get; set;
    }

    /// <summary>左侧横向边界。</summary>
    public float FromL { get; set; } = -1.0f;

    /// <summary>右侧横向边界。</summary>
    public float ToR { get; set; } = 1.0f;

    /// <summary>
    /// 判断目标单位是否处于矩形范围内。
    /// </summary>
    /// <param name="callSkillObject">施法单位。</param>
    /// <param name="testObject">被检测的目标单位。</param>
    /// <param name="targetPos">技能指向的目标位置（用于确定矩形朝向）。</param>
    /// <returns>目标处于矩形范围内返回 true。</returns>
    public bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos) {
        return Utility.IsInRange_Rect(
            testObject.Position,
            callSkillObject.Position,
            targetPos - callSkillObject.Position,
            NearClamp,
            FarClamp,
            FromL,
            ToR,
            bodyRadius: testObject.BodyRadius);
    }
}
