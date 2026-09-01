using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 施法意图载荷：一次待裁定的施法请求，挂在 <see cref="BattleUnit.CastInput"/> 上由战斗世界按帧消费。
/// 只带输入不带判定——射程与阵营在消费点按当时位置裁定；目标持 <see cref="BattleUnit"/> 引用，不做 ID 重解析。
/// </summary>
/// <param name="Skill">要施放的技能键。</param>
/// <param name="Target">单位目标；位置目标或无目标技能传 null。</param>
/// <param name="TargetPos">位置目标锚点，单位目标时玩家与回放传 null。AI 决策恒带锚点：单位目标下裁定不读它，但它随读条状态传给效果层。</param>
public readonly record struct CastIntent(SkillKeyId Skill, BattleUnit? Target, Vector2? TargetPos);
