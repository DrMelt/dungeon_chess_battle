using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;

namespace DungeonChessBattle.Game.MainScene.scenes;

/// <summary>
/// 目标循环选择器：持有选敌游标，按存活、阵营敌对过滤，并从循环游标之后选择下一敌方单位。
/// 游标失效时回退服务端权威焦点，仍不可用时从第一个敌方单位开始；没有存活敌方单位返回 0。
/// 纯交互启发式，不依赖会话引用，由输入控制器驱动，会话结束后重置。
/// </summary>
public sealed class TargetCycleSelector {
    /// <summary>循环目标游标，乐观推进容忍聚焦回包延迟，手动选择时对齐。</summary>
    private ushort _cursor;

    /// <summary>退出战斗时重置游标。</summary>
    public void Reset() => _cursor = 0;

    /// <summary>
    /// 计算下一敌方单位网络 ID；无存活敌方目标返回 0。
    /// </summary>
    /// <param name="units">场景全部单位展示视图。</param>
    /// <param name="localNetId">本地玩家单位网络 ID。</param>
    /// <param name="focusId">当前本地聚焦目标网络 ID，0 表示无。</param>
    /// <param name="resolveRelation">目标阵营 → 相对本地玩家的关系解析。</param>
    public ushort NextTarget(
        IReadOnlyList<IUnitUiView> units,
        ushort localNetId,
        ushort focusId,
        Func<IReadOnlyList<string>, CampRelation> resolveRelation) {
        var enemies = CollectLivingEnemies(units, localNetId, resolveRelation);
        if (enemies.Count == 0)
            return 0;

        int index = enemies.FindIndex(e => e.UnitId == _cursor);
        if (index < 0)
            index = enemies.FindIndex(e => e.UnitId == focusId);
        ushort next = enemies[(index + 1) % enemies.Count].UnitId;
        _cursor = next;
        return next;
    }

    /// <summary>收集与本地玩家阵营敌对且存活（Health&gt;0）的单位，按单位展示列表顺序排列。</summary>
    private static List<IUnitUiView> CollectLivingEnemies(
        IReadOnlyList<IUnitUiView> units,
        ushort localNetId,
        Func<IReadOnlyList<string>, CampRelation> resolveRelation) {
        List<IUnitUiView> enemies = [];
        foreach (var candidate in units) {
            if (candidate.UnitId == localNetId || candidate.IsDead)
                continue;
            if (resolveRelation(candidate.Camps) != CampRelation.Enemy)
                continue;
            enemies.Add(candidate);
        }
        return enemies;
    }
}
