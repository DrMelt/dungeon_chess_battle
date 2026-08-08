using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// Buff 数据基类，实现 IBuff。
/// 提供持续时间计时、叠加逻辑，并在每帧通过 ActionDuration/ActionEnd 虚方法派发持续效果与结束效果。
/// </summary>
public class BuffModel : IBuff {
    /// <summary>Buff 全局唯一 ID（对应配置表与 SyncBuffData.BuffTypeId）。</summary>
    public ushort BuffTypeId { get; set; } = 0;

    /// <summary>Buff 名称，作为叠加判定的唯一标识。</summary>
    public string BuffName { get; set; } = "";

    /// <summary>剩余持续时间（秒），降为负数或层数耗尽时 Buff 结束。</summary>
    public double Duration { get; set; } = 60;

    /// <summary>当前叠加层数。</summary>
    public int Superpositions { get; set; } = 1;

    /// <summary>最大可叠加层数。</summary>
    public int MaxSuperpositions { get; set; } = 1;

    /// <summary>是否仍生效。失效后将被从单位的 Buff 列表移除。</summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>释放该 Buff 的施法单位，可能为 null。</summary>
    public IUnitState? FromUnit {
        get; set;
    }

    /// <summary>
    /// 按帧推进 Buff 效果：执行持续效果、递减持续时间，并在结束时触发结束效果与失效标记。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    /// <param name="unitState">承载该 Buff 的目标单位。</param>
    public void Update(double deltaTime, IUnitState unitState) {
        if (!IsAlive)
            return;

        ActionDuration(deltaTime, unitState);

        Duration -= deltaTime;
        if (Duration < 0 || Superpositions <= 0) {
            ActionEnd(unitState);
            IsAlive = false;
        }
    }

    /// <summary>
    /// 每帧执行的持续效果（子类重写，如 HOT 回血、DOT 掉血）。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    /// <param name="unitState">承载该 Buff 的目标单位。</param>
    protected virtual void ActionDuration(double deltaTime, IUnitState unitState) {
    }

    /// <summary>
    /// Buff 结束时执行的效果（子类重写）。
    /// </summary>
    /// <param name="unitState">承载该 Buff 的目标单位。</param>
    protected virtual void ActionEnd(IUnitState unitState) {
    }

    /// <summary>
    /// 叠加另一层同类型 Buff：层数 +1（不超过最大层数），持续时间取两者较大值。
    /// </summary>
    /// <param name="other">用于叠加的另一个 Buff 实例。</param>
    public void AddSuperpositions(IBuff other) {
        Superpositions += 1;
        Superpositions = System.Math.Min(Superpositions, other.MaxSuperpositions);
        Duration = System.Math.Max(Duration, other.Duration);
    }
}
