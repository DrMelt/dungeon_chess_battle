using System.Numerics;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Interfaces {
    public interface IUnitSkill {
        float SkillSpellTime {
            get;
        }
        float GCDTime {
            get;
        }
        EnumSkillCanAdd SkillCanAdd {
            get;
        }
        bool NeedUnitTarget {
            get;
        }
        bool NeedPosTarget {
            get;
        }
        IUnitState CallSkillObject {
            get;
        }
        Vector3 TargetPos {
            get;
        }

        void UpdateSkill(double delta);
        bool IsCoolingdown();
        void SetSkill(IUnitState callSkillObject, IUnitState targetObject, Vector3? targetPos, IEnumerable<IUnitState> testObjects);
        void SpellBroked();
        bool CallSkillSpelling();
    }
}
