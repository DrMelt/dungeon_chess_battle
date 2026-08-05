using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 持续伤害 Buff（Damage Over Time），按秒对目标造成魔法伤害。
/// </summary>
public class BuffDOTModel : BuffModel {
    /// <summary>伤害类型。</summary>
    public DamageType DamageType {
        get; set;
    }

    /// <summary>每秒造成的伤害量。</summary>
    public float DamagePerSec { get; set; } = 100.0f;

    /// <summary>
    /// 每帧结算持续伤害：以施法者的魔法攻击系数换算每秒伤害后按帧扣除目标生命值。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    /// <param name="unitState">承受伤害的目标单位。</param>
    protected override void ActionDuration(double deltaTime, IUnitState unitState) {
        if (FromUnit == null)
            throw new InvalidOperationException("[BuffDOTModel] FromUnit has not been initialized.");
        unitState.TakeDamage((float)deltaTime * FromUnit.MagicDamageAmount(DamagePerSec), DamageType);
    }
}
