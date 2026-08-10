using System.Numerics;

namespace DungeonChessBattle.Battle.Interfaces {
    /// <summary>
    /// 技能范围判定接口，用于判断目标单位是否处于施法范围内。
    /// </summary>
    public interface IRangeChecker {
        /// <summary>
        /// 判断目标单位是否在施法范围内。
        /// </summary>
        /// <param name="callSkillObject">施法单位。</param>
        /// <param name="testObject">被检测的目标单位。</param>
        /// <param name="targetPos">技能指向的目标位置（用于确定朝向）。</param>
        /// <returns>目标处于范围内返回 true，否则返回 false。</returns>
        bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos);
    }
}
