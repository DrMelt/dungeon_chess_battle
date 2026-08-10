using System.Numerics;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Battle.Interfaces {
    /// <summary>
    /// 技能接口，定义技能施放状态机（冷却、读条、目标校验）与行为入口。
    /// </summary>
    public interface IUnitSkill {
        /// <summary>技能读条时间（秒）。</summary>
        float SkillSpellTime {
            get;
        }
        /// <summary>释放成功后触发的全局冷却时间（秒）。</summary>
        float GCDTime {
            get;
        }
        /// <summary>技能可释放的目标类型标志。</summary>
        SkillCanAdd SkillCanAdd {
            get;
        }
        /// <summary>是否需要锁定单位目标才能释放。</summary>
        bool NeedUnitTarget {
            get;
        }
        /// <summary>是否需要指定位置目标才能释放。</summary>
        bool NeedPosTarget {
            get;
        }
        /// <summary>当前施放该技能的单位。</summary>
        IUnitState? CallSkillObject {
            get;
        }
        /// <summary>技能指向的目标位置。</summary>
        Vector3 TargetPos {
            get;
        }

        /// <summary>
        /// 每帧推进技能的冷却计时与读条计时。
        /// </summary>
        /// <param name="delta">距上一帧的间隔时间（秒）。</param>
        void UpdateSkill(double delta);

        /// <summary>技能是否处于冷却中。</summary>
        bool IsCoolingdown();

        /// <summary>
        /// 发起技能释放，进行目标类型校验后进入读条状态。
        /// </summary>
        /// <param name="callSkillObject">施法单位。</param>
        /// <param name="targetObject">单位目标（NeedUnitTarget 时必填）。</param>
        /// <param name="targetPos">位置目标（NeedPosTarget 时必填）。</param>
        /// <param name="testObjects">可被技能命中的所有检测单位。</param>
        void SetSkill(IUnitState callSkillObject, IUnitState? targetObject, Vector3? targetPos, IEnumerable<IUnitState> testObjects);

        /// <summary>
        /// 打断当前施法（重置读条进度）。
        /// </summary>
        void SpellBroked();

        /// <summary>
        /// 判定读条是否完成且不在冷却中；完成时结算技能并返回 true。
        /// </summary>
        /// <returns>技能释放成功返回 true，否则返回 false。</returns>
        bool CallSkillSpelling();
    }
}
