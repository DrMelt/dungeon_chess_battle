using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class RangeResBaseGodot : Resource, IRangeRes {
    public virtual bool IsInRange(IUnitState callSkillObject, IUnitState testObject, System.Numerics.Vector3 targetPos) {
        return false;
    }
}
