using Godot;

namespace GameLogic.Interfaces {
    public interface IUnitState {
        Vector3 Position {
            get;
        }
        EnumCamp Camp {
            get;
        }
        string UnitStateName {
            get;
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

        void SpellNewSkill(GameLogic.Skills.UnitSkillBase skill);
        float TakeDamage(float damageAmount, Enum_DamageType damageType);
        float PhysicalDamageAmount(float physicalDamage);
        float MagicDamageAmount(float magicDamage);
        float CureAmount(float curePotency);
        float RestoreHealth(float health);
        void AddBuff(GameLogic.Buffs.BuffBase buff);
    }
}
