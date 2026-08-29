using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Movement;

namespace DungeonChessBattle.Battle.Logic.Movement;

/// <summary>
/// 领域层整场移动结算器：按逻辑步长对所有存活单位执行确定性移动。
/// 由 <see cref="BattleScene.Tick"/> 统一调用，在线与回放共用同一路径，
/// 基础公式经 <see cref="MovementResolver.Move"/>。
/// </summary>
public static class BattleMovementResolver {
    /// <summary>对全部存活、有朝向移动输入且速度非零的单位结算位移并更新朝向。</summary>
    public static void ResolveTurn(IReadOnlyList<BattleUnit> units, float dt, IMovementScene scene) {
        foreach (var unit in units) {
            if (unit.IsDead)
                continue;
            if (unit.MoveInput.LengthSquared() <= 0.0001f || unit.BaseSpeed <= 0f)
                continue;
            unit.Position = MovementResolver.Move(unit.Position, unit.MoveInput, unit.BaseSpeed,
                dt, unit.BodyRadius, scene, unit.UnitNetId);
            var dir = Vector2.Normalize(unit.MoveInput);
            if (unit.Direction != dir)
                unit.Direction = dir;
        }
    }
}
