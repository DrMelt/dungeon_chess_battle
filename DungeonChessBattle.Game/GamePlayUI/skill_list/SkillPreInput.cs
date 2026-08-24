using System;
using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Game.GamePlayUI.skill_list;

/// <summary>
/// 技能预输入缓冲：按键时若不满足施放条件则不打断，入队缓存一个技能；
/// 每帧尝试自动施放，入队超过窗口未施放自动作废。纯客户端输入辅助。
/// "能否施放"由注入的 canCast 委托决定，本组件不感知具体因素。
/// </summary>
/// <remarks>
/// 构造预输入缓冲。
/// </remarks>
/// <param name="canCast">给定施放意图（技能定义 + 目标 + 位置）能否施放的判定委托。</param>
/// <param name="caster">施放动作抽象。</param>
/// <param name="clock">单调时钟提供者，返回当前秒数，用于超时判定。</param>
/// <param name="windowSeconds">预输入有效窗口秒数。</param>
public sealed class SkillPreInput(Func<SkillDefinition, ushort, float, float, bool> canCast, ISkillCaster caster,
    Func<double>? clock = null, double windowSeconds = SkillPreInput.DefaultWindowSeconds) {
    /// <summary>预输入有效窗口，秒，可调默认值。</summary>
    public const double DefaultWindowSeconds = 0.5;

    private readonly Func<SkillDefinition, ushort, float, float, bool> _canCast = canCast ?? throw new ArgumentNullException(nameof(canCast));
    private readonly ISkillCaster _caster = caster ?? throw new ArgumentNullException(nameof(caster));
    private readonly Func<double> _clock = clock ?? (() => Godot.Time.GetTicksUsec() / 1_000_000.0);
    private readonly double _windowSeconds = windowSeconds;
    private Pending? _pending;

    private readonly record struct Pending(
        SkillDefinition Skill, ushort TargetNetId, float PosX, float PosZ, double ExpireAt);

    /// <summary>
    /// 提交施放意图：可施放立即施放；否则入队缓存，槽位单个，新按键替换旧缓存。
    /// 若当时处于读条等不可施放状态，当前动作不会被打断。
    /// </summary>
    public void Submit(SkillDefinition skill, ushort targetNetId, float posX, float posZ) {
        if (_canCast(skill, targetNetId, posX, posZ)) {
            _pending = null;
            _caster.Cast(skill.SkillId, targetNetId, posX, posZ);
            return;
        }
        _pending = new Pending(skill, targetNetId, posX, posZ, _clock() + _windowSeconds);
    }

    /// <summary>
    /// 每帧驱动：超时作废；判定可施放则自动施放；否则保留等待。
    /// </summary>
    public void Refresh() {
        if (_pending is not { } pending)
            return;
        if (_clock() >= pending.ExpireAt) {
            _pending = null;
            return;
        }
        if (!_canCast(pending.Skill, pending.TargetNetId, pending.PosX, pending.PosZ))
            return;
        _pending = null;
        _caster.Cast(pending.Skill.SkillId, pending.TargetNetId, pending.PosX, pending.PosZ);
    }

    /// <summary>清空预输入缓存，切换显示单位或退出战斗时调用。</summary>
    public void Clear() => _pending = null;
}
