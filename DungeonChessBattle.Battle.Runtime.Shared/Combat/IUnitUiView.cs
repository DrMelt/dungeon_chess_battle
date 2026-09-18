using System.Numerics;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Runtime.Shared.Combat;

/// <summary>
/// 单位展示层统一只读视图：在线战斗世界与回放重放共用，UI 一律按本接口取数。
/// 公共面（身份、数值、技能源、世界姿态）经 <see cref="ISkillCasterView"/> 组合，本接口只追加展示独有字段。
/// 位置语义与 <see cref="ISkillCasterView"/> 一致：Position 即本地 BattleScene 的结算位置，在线随服务端下行校正，回放纯本地重跑。
/// </summary>
public interface IUnitUiView : ISkillCasterView {
    /// <summary>碰撞半径，供展示层读取单位占位体积。</summary>
    float CollisionRadius {
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

    /// <summary>当前生效 Buff 视图。</summary>
    IReadOnlyList<IBuffView> Buffs {
        get;
    }
}
