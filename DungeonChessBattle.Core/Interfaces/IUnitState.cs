using System.Numerics;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Interfaces {
    /// <summary>
    /// 单位运行时状态接口，承载战斗属性、位置信息与伤害/治疗/Buff 行为。
    /// </summary>
    public interface IUnitState {
        /// <summary>单位当前世界坐标。</summary>
        Vector3 Position {
            get;
        }

        /// <summary>设置单位世界坐标；位置变化时由实现触发移动相关事件。</summary>
        void SetPosition(Vector3 position);

        /// <summary>当前移动速度。</summary>
        float MoveSpeed {
            get;
        }

        /// <summary>单位朝向向量。</summary>
        Vector3 LookAtDir {
            get;
            set;
        }

        /// <summary>
        /// 从配置源模型拷贝运行时数值（生命/速度/攻击/抗性系数等）。
        /// 用于服务端按单位配置（UnitConfig）初始化 Logic 模型，避免逐字段硬编码。
        /// </summary>
        /// <param name="source">配置源模型。</param>
        void CopyStatsFrom(IUnitState source);

        /// <summary>单位碰撞半径，用于技能范围判定。</summary>
        float BodyRadius {
            get;
        }
        /// <summary>单位所属阵营列表。</summary>
        List<string> Camps {
            get;
        }
        /// <summary>单位名称（调试与事件标识用）。</summary>
        string UnitStateName {
            get;
        }
        /// <summary>当前生命值。</summary>
        float Health {
            get;
            set;
        }

        /// <summary>最大生命值。</summary>
        float MaxHealth {
            get;
        }

        /// <summary>物理攻击基础系数（伤害倍率）。</summary>
        float PhysicalAttackBase {
            get;
        }

        /// <summary>物理伤害承受系数（减免倍率）。</summary>
        float PhysicalTakePercent {
            get;
        }

        /// <summary>魔法攻击基础系数（伤害倍率）。</summary>
        float MagicAttackBase {
            get;
        }

        /// <summary>魔法伤害承受系数（减免倍率）。</summary>
        float MagicTakePercent {
            get;
        }

        /// <summary>治疗强度系数（治疗量倍率）。</summary>
        float CureIntensity {
            get;
        }

        /// <summary>
        /// 发起新技能施放，自动打断当前正在施放的技能。
        /// </summary>
        /// <param name="skill">要施放的技能。</param>
        void SpellNewSkill(IUnitSkill skill);

        /// <summary>
        /// 结算一次伤害，按伤害类型应用对应抗性系数。
        /// </summary>
        /// <param name="damageAmount">原始伤害量。</param>
        /// <param name="damageType">伤害类型（物理/魔法）。</param>
        /// <returns>实际扣除的生命值。</returns>
        float TakeDamage(float damageAmount, DamageType damageType);

        /// <summary>
        /// 计算物理伤害的实际数值（基础倍率换算）。
        /// </summary>
        /// <param name="physicalDamage">原始物理伤害量。</param>
        /// <returns>物理攻击加成后的伤害数值。</returns>
        float PhysicalDamageAmount(float physicalDamage);

        /// <summary>
        /// 计算魔法伤害的实际数值（基础倍率换算）。
        /// </summary>
        /// <param name="magicDamage">原始魔法伤害量。</param>
        /// <returns>魔法攻击加成后的伤害数值。</returns>
        float MagicDamageAmount(float magicDamage);

        /// <summary>
        /// 计算治疗量（治疗强度换算）。
        /// </summary>
        /// <param name="curePotency">原始治疗量。</param>
        /// <returns>治疗强度加成后的治疗数值。</returns>
        float CureAmount(float curePotency);

        /// <summary>
        /// 恢复生命值，不超过最大生命值。
        /// </summary>
        /// <param name="health">期望恢复量。</param>
        /// <returns>实际恢复的生命值。</returns>
        float RestoreHealth(float health);

        /// <summary>
        /// 添加一个 Buff；同类型已存在时触发叠加。
        /// </summary>
        /// <param name="buff">要添加的 Buff。</param>
        void AddBuff(IBuff buff);

        /// <summary>
        /// 按帧更新全部 Buff，并移除已失效的 Buff。
        /// </summary>
        /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
        void UpdateBuffList(double deltaTime);
    }
}
