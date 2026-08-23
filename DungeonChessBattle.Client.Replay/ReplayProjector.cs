using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Client.Replay;

/// <summary>
/// 回放状态投影器：把战斗世界领域单位投影到 <see cref="ReplayUnitView"/> 展示模型。
/// 阶段变化不投影（回放端阶段由引擎直接读取）。在 BattleScene.Tick 末尾被调用。
/// </summary>
internal sealed class ReplayProjector(IReadOnlyDictionary<ushort, ReplayUnitView> views) : IBattleProjector {
    /// <inheritdoc />
    public void Project(IReadOnlyList<BattleUnit> units, BattlePhase phase) {
        foreach (var unit in units) {
            if (!views.TryGetValue(unit.UnitNetId, out var view))
                continue;
            view.Position = unit.Position;
            view.Direction = unit.Direction;
            view.Health = unit.Health;
            view.MaxHealth = unit.MaxHealth;
            view.SkillCasting = unit.SkillCasting.Id;
            view.IsDead = unit.Health <= 0f;
        }
    }
}
