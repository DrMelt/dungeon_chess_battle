using System.Numerics;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GameConfig;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 敌人大脑：服务端每 tick 为敌方单位驱动移动与施法。
/// 敌方单位无玩家控制器，由本类在房间线程直接注入移动输入并调用战斗编排发起读条。
/// 策略保持简单：锁定最近存活玩家目标，超出施法距离则逼近，冷却就绪且读条空闲时施放首个可用技能。
/// </summary>
/// <remarks>
/// 构造敌人大脑。
/// </remarks>
/// <param name="battleRoom">本房间的战斗编排实例。</param>
/// <param name="logger">日志器。</param>
public sealed class EnemyBrain(BattleRoom battleRoom, ILogger<EnemyBrain> logger) {
    /// <summary>敌人大脑日志器。</summary>
    private readonly ILogger<EnemyBrain> _logger = logger;

    /// <summary>战斗编排引用，驱动读条与结算。</summary>
    private readonly BattleRoom _battleRoom = battleRoom;

    /// <summary>敌方开始施法的最远距离，超出则移动逼近。</summary>
    private const float AttackRange = 10f;

    /// <summary>
    /// 服务端每逻辑 tick 调用，驱动全部存活敌人。仅房间线程调用。
    /// 必须在 EntityManager.Update() 之前执行，确保移动输入先于单位位移结算写入。
    /// </summary>
    /// <param name="enemies">本房间的敌人 Pawn 列表。</param>
    /// <param name="allPawns">本房间全部 Pawn，用作寻找目标。</param>
    public void Tick(IReadOnlyList<UnitPawn> enemies, IReadOnlyList<UnitPawn> allPawns) {
        if (enemies.Count == 0)
            return;

        for (int i = enemies.Count - 1; i >= 0; i--) {
            var enemy = enemies[i];
            if (enemy.UnitState.Value != 0)
                continue;
            TickEnemy(enemy, allPawns);
        }
    }

    /// <summary>驱动单个敌人：寻敌、移动或施法。</summary>
    private void TickEnemy(UnitPawn enemy, IReadOnlyList<UnitPawn> allPawns) {
        var target = FindNearestAlivePlayer(enemy, allPawns);
        if (target == null) {
            enemy.SetMovementInput(Vector2.Zero);
            return;
        }

        float distance = Vector2.Distance(enemy.Position.Value, target.Position.Value);
        if (distance > AttackRange) {
            // 逼近目标
            enemy.SetMovementInput(target.Position.Value - enemy.Position.Value);
            return;
        }

        // 已进入施法范围：停止移动，读条空闲且冷却就绪时施法
        enemy.SetMovementInput(Vector2.Zero);
        TryCastSkill(enemy, target);
    }

    /// <summary>锁定距离最近且存活的玩家单位；无目标返回 null。</summary>
    private static UnitPawn? FindNearestAlivePlayer(UnitPawn enemy, IReadOnlyList<UnitPawn> allPawns) {
        UnitPawn? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var pawn in allPawns) {
            if (pawn == enemy || pawn.UnitState.Value != 0)
                continue;
            if (pawn.Camp.Value == CampConstants.CampBoss)
                continue;

            float distance = Vector2.DistanceSquared(enemy.Position.Value, pawn.Position.Value);
            if (distance < nearestDistance) {
                nearestDistance = distance;
                nearest = pawn;
            }
        }
        return nearest;
    }

    /// <summary>尝试施放首个可用的对战技能；GCD 读条空闲且技能冷却就绪才发起。</summary>
    private void TryCastSkill(UnitPawn enemy, UnitPawn target) {
        if (enemy.GcdRemaining.Value > 0f || enemy.SkillCasting.Value != 0)
            return;

        foreach (ushort skillId in enemy.SkillIds) {
            var skill = GameConfigDB.GetSkillById(skillId);
            if (skill == null)
                continue;

            bool castStarted = _battleRoom.BeginCast(enemy, skillId, target, target.Position.Value);
            if (castStarted) {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("[EnemyBrain] {Enemy} starts casting skill {SkillId} on {Target}.",
                        enemy.UnitName.Value, skillId, target.UnitName.Value);
                return;
            }
        }
    }
}
