using System.Numerics;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Interfaces {
    public interface IUnitState {
        Vector3 Position {
            get;
        }
        float BodyRadius {
            get;
        }
        List<string> Camps {
            get;
        }
        string UnitStateName {
            get;
        }
        float Health {
            get;
            set;
        }
        float PhysicalAttackBase {
            get;
        }
        float MagicAttackBase {
            get;
        }
        float CureIntensity {
            get;
        }

        void SpellNewSkill(IUnitSkill skill);
        float TakeDamage(float damageAmount, Enum_DamageType damageType);
        float PhysicalDamageAmount(float physicalDamage);
        float MagicDamageAmount(float magicDamage);
        float CureAmount(float curePotency);
        float RestoreHealth(float health);
        void AddBuff(IBuff buff);
        void UpdateBuffList(double deltaTime);
    }
}
