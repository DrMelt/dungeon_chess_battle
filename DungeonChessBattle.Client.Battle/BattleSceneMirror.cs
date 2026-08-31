using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Logic;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 在线客户端本地战斗世界与网络载体之间的状态搬运。
/// `Pull` 把 `UnitPawn` 的 SyncVar 读数覆写进领域 `BattleUnit`，是当前唯一被调用的方向；
/// `Flush` 供本地结算结果回写 SyncVar，当前无调用点。两份方向共用同一字段集合，保证读写对称。
/// `UnitPawn` 未标 `SyncFlags.Interpolated`，LES 的 A/B 插值通道未启用，一律读写 `Value`。
/// </summary>
internal sealed class BattleSceneMirror {
    /// <summary>回填：把网络 UnitPawn SyncVar 读数覆写进本地 BattleUnit，作为展示与判定的取数源。</summary>
    public static void Pull(BattleScene scene, IEnumerable<UnitPawn> pawns) {
        foreach (var unit in scene.BattleUnits) {
            foreach (var pawn in pawns) {
                if (pawn.Id != unit.UnitId)
                    continue;

                unit.Position = pawn.Position.Value;
                unit.Direction = pawn.Direction.Value;

                unit.Health = pawn.Health.Value;
                unit.MaxHealth = pawn.MaxHealth.Value;
                unit.BodyRadius = pawn.BodyRadius.Value;
                unit.BaseSpeed = pawn.BaseSpeed.Value;
                unit.PhysicalAttackBase = pawn.PhysicalAttackBase.Value;
                unit.PhysicalTakePercent = pawn.PhysicalTakePercent.Value;
                unit.MagicAttackBase = pawn.MagicAttackBase.Value;
                unit.MagicTakePercent = pawn.MagicTakePercent.Value;
                unit.CureIntensity = pawn.CureIntensity.Value;
                unit.SkillCasting = string.IsNullOrEmpty(pawn.SkillCasting.Value) ? default : new SkillKeyId(pawn.SkillCasting.Value);
                unit.SkillCastRemaining = pawn.SkillCastRemaining.Value;
                break;
            }
        }
    }

    /// <summary>回写：把本地 BattleUnit 模拟结果写回全部 UnitPawn SyncVar。</summary>
    public static void Flush(BattleScene scene, IEnumerable<UnitPawn> pawns) {
        foreach (var unit in scene.BattleUnits) {
            foreach (var pawn in pawns) {
                if (pawn.Id == unit.UnitId) {
                    pawn.Position.Value = unit.Position;
                    pawn.Direction.Value = unit.Direction;
                    pawn.Health.Value = unit.Health;
                    pawn.MaxHealth.Value = unit.MaxHealth;
                    pawn.BodyRadius.Value = unit.BodyRadius;
                    pawn.BaseSpeed.Value = unit.BaseSpeed;
                    pawn.PhysicalAttackBase.Value = unit.PhysicalAttackBase;
                    pawn.PhysicalTakePercent.Value = unit.PhysicalTakePercent;
                    pawn.MagicAttackBase.Value = unit.MagicAttackBase;
                    pawn.MagicTakePercent.Value = unit.MagicTakePercent;
                    pawn.CureIntensity.Value = unit.CureIntensity;
                    pawn.SkillCasting.Value = unit.SkillCasting.Id;
                    pawn.SkillCastRemaining.Value = unit.SkillCastRemaining;
                }
            }
        }
    }
}
