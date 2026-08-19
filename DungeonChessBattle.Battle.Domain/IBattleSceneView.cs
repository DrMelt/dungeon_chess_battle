using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain;

/// <summary>
/// 战场查询视图：AI 决策只读入口，不含写通道与推进方法。
/// 单位权威状态经 <see cref="IBattleUnit"/> 只读成员读取；实现方为 <see cref="IBattleScene"/>。
/// </summary>
public interface IBattleSceneView {
    /// <summary>战斗阶段，经 <see cref="IBattleRoom"/> 读取载体权威。</summary>
    BattlePhase CurrentPhase {
        get;
    }

    /// <summary>战斗已运行的秒数，Running 期间累加。</summary>
    float ElapsedTime {
        get;
    }

    /// <summary>本房间全部战斗单位。AI 决策只读使用，禁止写。</summary>
    IReadOnlyList<IBattleUnit> Units {
        get;
    }

    /// <summary>按网络 ID 查单位，不存在返回 null。</summary>
    IBattleUnit? FindUnit(ushort netId);
}
