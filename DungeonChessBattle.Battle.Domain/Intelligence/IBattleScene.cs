using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain.Intelligence;

/// <summary>
/// 敌人决策可查询的本帧战场只读视图：单位列表与按 ID 查询。
/// 只暴露领域类型，不接触网络载体；实现方负责线程安全与只读保证。
/// </summary>
public interface IBattleScene {
    /// <summary>本房间全部战斗单位，只读。</summary>
    IReadOnlyList<IBattleUnit> Units {
        get;
    }

    /// <summary>按网络 ID 查单位，不存在返回 null。</summary>
    IBattleUnit? FindUnit(ushort netId);
}
