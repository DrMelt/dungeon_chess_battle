using System.Numerics;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 技能数据抽象基类，实现 IUnitSkill 的读条、冷却、目标校验与释放状态机。
/// 子类通过组合 SkillModel 的派生模型承载具体技能效果数值。
/// </summary>
public abstract class SkillModel : IUnitSkill {
    /// <summary>技能读条时间（秒）。</summary>
    public float SkillSpellTime { get; set; } = 2.0f;

    /// <summary>技能自身冷却时间（秒）。</summary>
    public float SkillCooldownTime { get; set; } = 3.0f;

    /// <summary>释放成功后触发的全局冷却时间（秒）。</summary>
    public float GCDTime { get; set; } = 3.0f;

    /// <summary>是否需要锁定单位目标才能释放。</summary>
    public bool NeedUnitTarget {
        get; set;
    }

    /// <summary>是否需要指定位置目标才能释放。</summary>
    public bool NeedPosTarget {
        get; set;
    }

    /// <summary>技能可释放的目标类型标志。</summary>
    public SkillCanAdd SkillCanAdd { get; set; } = SkillCanAdd.None;

    /// <summary>已读条时间（秒）。</summary>
    public float SkillSpelledTime {
        get; set;
    }

    /// <summary>剩余冷却时间（秒）。</summary>
    public float SkillCoolingTime {
        get; set;
    }

    /// <summary>读条进度（0~1）。</summary>
    public float SkillSpellProgress => SkillSpelledTime / SkillSpellTime;

    /// <summary>当前施放该技能的单位。</summary>
    public IUnitState? CallSkillObject {
        get; protected set;
    }

    /// <summary>技能指向的单位目标。</summary>
    protected IUnitState? TargetObject {
        get; set;
    }

    /// <summary>技能指向的目标位置。</summary>
    public Vector3 TargetPos {
        get; protected set;
    }

    /// <summary>
    /// 设置技能目标位置（供读条完成后的范围伤害结算使用，不触发施法状态机）。
    /// </summary>
    /// <param name="pos">目标位置。</param>
    public void SetTargetPosition(Vector3 pos) {
        TargetPos = pos;
    }

    /// <summary>可被技能命中的所有检测单位。</summary>
    protected List<IUnitState> TestObjects { get; set; } = [];

    /// <summary>
    /// 每帧推进技能的冷却计时与读条计时。
    /// </summary>
    /// <param name="delta">距上一帧的间隔时间（秒）。</param>
    public void UpdateSkill(double delta) {
        SkillCoolingTime -= (float)delta;
        SkillSpelledTime += (float)delta;
    }

    /// <summary>技能是否处于冷却中。</summary>
    public bool IsCoolingdown() {
        return SkillCoolingTime > 0;
    }

    /// <summary>
    /// 根据目标与施法单位的阵营关系计算可释放类型标志。
    /// </summary>
    /// <param name="callSkillObject">施法单位。</param>
    /// <param name="testObject">目标单位。</param>
    /// <returns>同阵营返回 <see cref="SkillCanAdd.Same"/>，敌阵营返回 <see cref="SkillCanAdd.Different"/>。</returns>
    private static SkillCanAdd SkillAddEnum(IUnitState callSkillObject, IUnitState testObject) {
        SkillCanAdd addEnum = SkillCanAdd.None;
        bool isSameCamp = callSkillObject.Camps.Any(c => testObject.Camps.Contains(c));

        if (isSameCamp) {
            addEnum |= SkillCanAdd.Same;
        }
        else {
            addEnum |= SkillCanAdd.Different;
        }

        return addEnum;
    }

    /// <summary>
    /// 发起技能释放：进行目标类型校验后进入读条状态，并通知施法单位开始施法。
    /// </summary>
    /// <param name="callSkillObject">施法单位。</param>
    /// <param name="targetObject">单位目标（NeedUnitTarget 时非空且需满足阵营限制）。</param>
    /// <param name="targetPos">位置目标（NeedPosTarget 时必填）。</param>
    /// <param name="testObjects">可被技能命中的所有检测单位。</param>
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

    /// <summary>
    /// 打断当前施法（重置读条进度）。
    /// </summary>
    public void SpellBroked() {
        SkillSpelledTime = 0;
    }

    /// <summary>
    /// 判定读条是否完成且不在冷却中；完成时结算技能并返回 true。
    /// </summary>
    /// <returns>技能释放成功返回 true，否则返回 false。</returns>
    public bool CallSkillSpelling() {
        if (!IsCoolingdown() && SkillSpelledTime >= SkillSpellTime) {
            ResetSpelledSkill();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 释放完成后重置技能状态：进入冷却并清零读条。
    /// </summary>
    protected void ResetSpelledSkill() {
        SkillCoolingTime = SkillCooldownTime;
        SkillSpelledTime = 0;
    }
}
