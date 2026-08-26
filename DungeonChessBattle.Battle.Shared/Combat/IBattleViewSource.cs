namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗展示数据源契约：在线状态镜像与回放重放共用，UI 一律按本契约取数。
/// 在线经 RoomBattleStateMirror 适配，回放经 ReplayEngine 适配。
/// </summary>
public interface IBattleViewSource {
    /// <summary>全部展示单位视图。</summary>
    IReadOnlyList<IUnitUiView> Units {
        get;
    }

    /// <summary>按网络 ID 查展示单位，不存在返回 null。</summary>
    IUnitUiView? FindUnit(ushort netId);
}
