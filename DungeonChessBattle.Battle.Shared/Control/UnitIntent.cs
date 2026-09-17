using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared.Control;

/// <summary>
/// 单位施法意图：一次待裁定的施法请求，纯数据不持引用，目标以单位标识表达。
/// 目标在投递点按标识解析，射程与冷却一律交消费点按当时状态裁定。
/// </summary>
/// <param name="Skill">要施放的技能键。</param>
/// <param name="TargetUnitId">单位目标身份，<see cref="UnitId.None"/> 表示无单位目标。</param>
/// <param name="TargetPos">位置目标锚点，无位置目标时为 null。</param>
public readonly record struct UnitCastIntent(SkillKeyId Skill, UnitId TargetUnitId, Vector2? TargetPos);

/// <summary>
/// 单位当帧意图：意图源的产出形状，玩家输入与自治决策同形，随 <c>BattleScene.Tick</c> 末作废。
/// 零移动向量即静止，无施法意图即不投施法。
/// </summary>
/// <param name="Move">移动方向。</param>
/// <param name="Cast">当帧施法意图。</param>
public readonly record struct UnitIntent(Vector2 Move, UnitCastIntent? Cast) {
    /// <summary>静止且无施法：缺省意图。</summary>
    public static UnitIntent Idle => default;

    /// <summary>朝给定方向移动，不投施法。</summary>
    public static UnitIntent MoveTo(Vector2 direction) => new(direction, null);

    /// <summary>施放技能，不投移动。</summary>
    public static UnitIntent CastSkill(SkillKeyId skill, UnitId targetUnitId, Vector2 targetPos) =>
        new(Vector2.Zero, new UnitCastIntent(skill, targetUnitId, targetPos));
}
