using System.Numerics;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public abstract class SkillModel : IUnitSkill {
    public float SkillSpellTime { get; set; } = 2.0f;
    public float SkillCooldownTime { get; set; } = 3.0f;
    public float GCDTime { get; set; } = 3.0f;

    public bool NeedUnitTarget {
        get; set;
    }
    public bool NeedPosTarget {
        get; set;
    }

    public EnumSkillCanAdd SkillCanAdd { get; set; } = EnumSkillCanAdd.None;

    public float SkillSpelledTime {
        get; set;
    }
    public float SkillCoolingTime {
        get; set;
    }

    public float SkillSpellProgress => SkillSpelledTime / SkillSpellTime;

    public IUnitState? CallSkillObject {
        get; protected set;
    }
    protected IUnitState? TargetObject {
        get; set;
    }
    public Vector3 TargetPos {
        get; protected set;
    }
    protected List<IUnitState> TestObjects { get; set; } = [];

    public void UpdateSkill(double delta) {
        SkillCoolingTime -= (float)delta;
        SkillSpelledTime += (float)delta;
    }

    public bool IsCoolingdown() {
        return SkillCoolingTime > 0;
    }

    private static EnumSkillCanAdd SkillAddEnum(IUnitState callSkillObject, IUnitState testObject) {
        EnumSkillCanAdd addEnum = EnumSkillCanAdd.None;
        bool isSameCamp = callSkillObject.Camps.Any(c => testObject.Camps.Contains(c));

        if (isSameCamp) {
            addEnum |= EnumSkillCanAdd.Same;
        }
        else {
            addEnum |= EnumSkillCanAdd.Different;
        }

        return addEnum;
    }

    public void SetSkill(IUnitState callSkillObject, IUnitState? targetObject, Vector3? targetPos, IEnumerable<IUnitState> testObjects) {
        if (NeedUnitTarget) {
            if (targetObject == null)
                return;

            if (!SkillAddEnum(callSkillObject, targetObject).HasFlag(SkillCanAdd))
                return;
        }

        SkillSpelledTime = 0;

        CallSkillObject = callSkillObject;
        TargetObject = targetObject;
        if (targetPos.HasValue) {
            TargetPos = targetPos.Value;
        }

        TestObjects = [.. testObjects];

        callSkillObject.SpellNewSkill(this);
    }

    public void SpellBroked() {
        SkillSpelledTime = 0;
    }

    public bool CallSkillSpelling() {
        if (!IsCoolingdown() && SkillSpelledTime >= SkillSpellTime) {
            ResetSpelledSkill();
            return true;
        }

        return false;
    }

    protected void ResetSpelledSkill() {
        SkillCoolingTime = SkillCooldownTime;
        SkillSpelledTime = 0;
    }
}
