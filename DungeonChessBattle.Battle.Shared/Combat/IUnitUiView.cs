using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// Buff 展示只读视图：在线战斗世界与回放重放共用，仅暴露 UI 展示所需字段。
/// </summary>
public interface IBuffUiView {
    /// <summary>Buff 类型 ID。</summary>
    ushort BuffTypeId {
        get;
    }

    /// <summary>当前叠加层数。</summary>
    int Stacks {
        get;
    }

    /// <summary>最大叠加层数。</summary>
    int MaxStacks {
        get;
    }

    /// <summary>剩余持续时间，秒。</summary>
    double Remaining {
        get;
    }

    /// <summary>来源施法单位，None 表示无来源。</summary>
    UnitId SourceUnitId {
        get;
    }

    /// <summary>伤害类型，仅供着色；非伤害 Buff 为 None。</summary>
    DamageType DamageType {
        get;
    }
}

/// <summary>
/// 单位展示层统一只读视图：在线战斗世界与回放重放共用，UI 一律按本契约取数。
/// 在公共面（身份、数值、技能源）之上追加展示所需字段。
/// 位置语义与 <see cref="ISkillCasterView"/> 一致：Position 即本地 BattleScene 的结算位置，在线随服务端下行校正，回放纯本地重跑。
/// </summary>
public interface IUnitUiView : IUnitIdentityView, ICombatValuesView, ISkillSource {
    /// <summary>当前世界位置，XZ 平面，语义见本接口注释。</summary>
    Vector2 Position {
        get;
    }

    /// <summary>碰撞半径，展示与空间互斥用。</summary>
    float BodyRadius {
        get;
    }

    /// <summary>最大生命值。</summary>
    float MaxHealth {
        get;
    }

    /// <summary>当前施法剩余读条时间，秒。</summary>
    float SkillCastRemaining {
        get;
    }

    /// <summary>当前朝向方向向量，XZ 平面。</summary>
    Vector2 Direction {
        get;
    }

    /// <summary>当前生效 Buff 展示视图。</summary>
    IReadOnlyList<IBuffUiView> Buffs {
        get;
    }
}
