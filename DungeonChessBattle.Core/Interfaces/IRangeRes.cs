using System.Numerics;

namespace DungeonChessBattle.Core.Interfaces {
    public interface IRangeRes {
        bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos);
    }
}
