using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 单位世界姿态只读视图：碰撞半径与当前世界位置（XZ 平面）。
/// 供距离/范围判定与空间互斥使用，被 <see cref="ISkillCasterView"/> 组合。
/// Position 为承载方当前的位置读数：服务端与回放是本地结算值，在线客户端是服务端下行回填值。
/// 与展示层 <see cref="IUnitUiView"/> 同名字段语义一致。
/// </summary>
public interface IWorldPoseView {
    /// <summary>碰撞半径，供技能范围判定与空间互斥使用。</summary>
    float BodyRadius {
        get;
    }

    /// <summary>当前世界位置，XZ 平面，供距离判定使用。</summary>
    Vector2 Position {
        get;
    }
}

/// <summary>
/// 施法判定只读视图：SkillCastValidator 聚合的字段子集，服务端权威单位与客户端预判共用。
/// 在公共面（身份、数值、技能源）之上追加世界姿态（<see cref="IWorldPoseView"/>），
/// 不继承仇恨通道与结算快照。服务端结算权威仍在 <see cref="BattleUnit"/>。
/// </summary>
public interface ISkillCasterView : IUnitIdentityView, ICombatValuesView, ISkillSource, IWorldPoseView {
}
