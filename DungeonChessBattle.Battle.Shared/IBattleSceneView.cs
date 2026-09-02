using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared;

/// <summary>
/// 战场查询视图：AI 决策只读入口，不含写通道与推进方法。
/// 单位权威状态经 <see cref="IBattleUnitView"/> 只读成员读取；实现方为 BattleScene。
/// </summary>
public interface IBattleSceneView {
    /// <summary>战斗阶段只读视图；推进经实现类的阶段写通道，视图本身不提供写。</summary>
    BattlePhase CurrentPhase {
        get;
    }

    /// <summary>战斗已运行的秒数，Running 期间累加。</summary>
    float ElapsedTime {
        get;
    }

    /// <summary>本房间全部战斗单位只读视图。AI 决策只读使用，禁止写。</summary>
    IReadOnlyList<IBattleUnitView> Units {
        get;
    }

    /// <summary>按单位 ID 查单位只读视图，不存在返回 null。</summary>
    IBattleUnitView? FindUnit(UnitId unitId);

    /// <summary>
    /// 施法可行性权威判定：阵营关系、射程与冷却口径由实现方自持，内容侧只取结果。
    /// </summary>
    /// <param name="caster">施法单位只读视图。</param>
    /// <param name="skill">待判定的技能定义。</param>
    /// <param name="target">已解析的单位目标；无单位目标需求时传 null。</param>
    /// <param name="targetPos">已解析的位置目标；无位置目标需求时传 null。</param>
    bool CanCast(ISkillCasterView caster, SkillDefinition skill, ISkillCasterView? target, Vector2? targetPos);
}
