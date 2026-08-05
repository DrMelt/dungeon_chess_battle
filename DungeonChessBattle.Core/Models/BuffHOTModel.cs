using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 持续治疗 Buff（Heal Over Time），按秒恢复目标生命值。
/// </summary>
public class BuffHOTModel : BuffModel {
    /// <summary>每秒恢复的生命值。</summary>
    public float HealthPerSec { get; set; } = 100.0f;

    /// <summary>
    /// 每帧结算持续治疗：以施法者的治疗强度系数换算每秒治疗量后按帧恢复目标生命值。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    /// <param name="unitState">接受治疗的目标单位。</param>
    protected override void ActionDuration(double deltaTime, IUnitState unitState) {
        if (FromUnit == null)
            throw new InvalidOperationException("[BuffHOTModel] FromUnit has not been initialized.");
        unitState.RestoreHealth(HealthPerSec * (float)deltaTime * FromUnit.CureIntensity);
    }
}
