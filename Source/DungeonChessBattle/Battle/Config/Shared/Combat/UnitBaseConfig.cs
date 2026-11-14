namespace DungeonChessBattle.Battle.Config.Shared.Combat;

/// <summary>
/// 单位基础状态：不变基础数值，由 UnitConfig 装配携带，运行时实体直接引用本实例。
/// 全为静态配置数据，不含运行时可变项。
/// </summary>
/// <param name="MaxHealth">最大生命值。</param>
/// <param name="CollisionRadius">碰撞半径，供位移解算的互斥、障碍推挤与边界约束使用。</param>
/// <param name="HitRadius">判定半径，供技能射程与范围命中判定使用。</param>
/// <param name="BaseSpeed">基础移动速度。</param>
/// <param name="PhysicalAttackBase">物理攻击基础系数即伤害倍率。</param>
/// <param name="PhysicalTakePercent">物理伤害承受系数即减免倍率。</param>
/// <param name="MagicAttackBase">魔法攻击基础系数即伤害倍率。</param>
/// <param name="MagicTakePercent">魔法伤害承受系数即减免倍率。</param>
/// <param name="CureIntensity">治疗强度系数即治疗倍率。</param>
public sealed record UnitBaseConfig(
    float MaxHealth,
    float CollisionRadius,
    float HitRadius,
    float BaseSpeed,
    float PhysicalAttackBase,
    float PhysicalTakePercent,
    float MagicAttackBase,
    float MagicTakePercent,
    float CureIntensity);
