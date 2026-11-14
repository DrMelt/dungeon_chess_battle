using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 单位世界姿态只读视图：判定半径、当前世界位置与朝向，XZ 平面。
/// 供距离/范围判定与展示驱动使用，被 <see cref="ISkillCasterView"/> 组合。
/// Position 与 Direction 为承载方当前的读数：服务端与回放是本地结算值，在线客户端是服务端下行回填值。
/// </summary>
public interface IWorldPoseView {
    /// <summary>判定半径，供技能射程与范围判定使用。</summary>
    float HitRadius {
        get;
    }

    /// <summary>当前世界位置，XZ 平面，供距离判定与展示驱动使用。</summary>
    Vector2 Position {
        get;
    }

    /// <summary>当前朝向方向向量，XZ 平面，供展示驱动使用。</summary>
    Vector2 Direction {
        get;
    }
}

/// <summary>
/// 施法判定只读视图：SkillCastValidator 聚合的字段子集，服务端与回放权威单位及自治决策共用。
/// 在公共面（身份、数值、技能源）之上追加世界姿态（<see cref="IWorldPoseView"/>），
/// 不继承仇恨通道与结算快照。服务端结算权威仍在运行时层的单位实体。
/// </summary>
public interface ISkillCasterView : IUnitIdentityView, ICombatValuesView, ISkillSource, IWorldPoseView {
}
