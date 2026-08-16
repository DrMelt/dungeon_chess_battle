using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Intelligence;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 敌人决策场景的服务端实现：把每帧候选池与目标索引聚合为只读视图。
/// 房间线程内每帧构建，决策输入只读，不接触网络载体。
/// </summary>
internal sealed class BattleScene(IReadOnlyList<IBattleUnit> units, IReadOnlyDictionary<ushort, IBattleUnit> byId)
    : IBattleScene {
    private readonly IReadOnlyList<IBattleUnit> _units = units;
    private readonly IReadOnlyDictionary<ushort, IBattleUnit> _byId = byId;

    /// <inheritdoc />
    public IReadOnlyList<IBattleUnit> Units => _units;

    /// <inheritdoc />
    public IBattleUnit? FindUnit(ushort netId) =>
        _byId.TryGetValue(netId, out var unit) ? unit : null;
}
