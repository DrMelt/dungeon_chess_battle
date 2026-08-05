using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// RoomBattleClient 的 LES 实体创建回调与本地模型构建工具。
/// </summary>
public partial class RoomBattleClient {
    /// <summary>房间实体创建回调：缓存房间与当前房间 ID。</summary>
    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        lock (_lock) {
            _roomEntity = entity;
            _currentRoomId = entity.RoomId.Value;
            _roomPawns.Clear();
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Room entity created: {RoomId}", entity.RoomId.Value);
    }

    /// <summary>单位实体创建回调：缓存 Pawn 并订阅其事件。</summary>
    private void OnPawnEntityCreated(UnitPawn pawn) {
        var unitName = pawn.UnitName.Value;
        lock (_lock) {
            _roomPawns.Add(pawn);
        }

        // 订阅 UnitPawn 事件
        pawn.HealthChanged += (u, newHealth, oldHealth) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitHealthChanged?.Invoke(u.UnitName.Value, newHealth, oldHealth));
        pawn.UnitDied += (u) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitDied?.Invoke(u.UnitName.Value));
        pawn.BuffAdded += (u, buff) => {
            var eventData = MapBuffData(buff);
            _pendingEventInvocations.Enqueue(() =>
                UnitBuffAdded?.Invoke(u.UnitName.Value, eventData));
        };
        pawn.BuffRemoved += (u, buff) => {
            var eventData = MapBuffData(buff);
            _pendingEventInvocations.Enqueue(() =>
                UnitBuffRemoved?.Invoke(u.UnitName.Value, eventData));
        };

        // 触发 OnUnitCreated 事件（通知 UI 层）
        var roomId = _currentRoomId;
        if (roomId != null) {
            _pendingEventInvocations.Enqueue(() =>
                OnUnitCreated?.Invoke(roomId, unitName, pawn.Camp.Value));
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] UnitPawn entity created: {UnitName}, Camp={Camp}, Pos={Position}",
                unitName, pawn.Camp.Value, pawn.Position.Value);
    }

    /// <summary>玩家实体创建回调：查找并缓存本地玩家的控制器。</summary>
    private void OnPlayerEntityCreated(PlayerRoomEntity player) {
        // 尝试查找并保存本地玩家的 UnitController（用于 SubmitPlayerInput）
        if (_entityManager != null && _localController == null) {
            _localController = _entityManager.GetPlayerController<UnitController>();
            if (_localController != null && _logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomBattleClient] Local UnitController found for player: {PlayerName}", player.PlayerName.Value);
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Player entity created: {PlayerName}", player.PlayerName.Value);
    }

    /// <summary>将同步 Buff 数据映射为 UI 事件使用的轻量数据结构。</summary>
    private static BuffEventData MapBuffData(SyncBuffData buff) => new() {
        BuffTypeId = buff.BuffTypeId,
        RemainingDuration = buff.RemainingDuration,
        StackCount = buff.StackCount,
        DamageType = buff.DamageType,
    };

    /// <summary>按单位名称查找本房间的 Pawn 实体。</summary>
    private UnitPawn? FindPawnByName(string unitName) {
        lock (_lock) {
            return _roomPawns.Find(p => p.UnitName.Value == unitName);
        }
    }

    /// <summary>将 Pawn 实体的同步数值构建为本地单位模型。</summary>
    private static UnitModel BuildModelFromPawn(UnitPawn p) {
        return new UnitModel {
            UnitStateName = p.UnitName.Value,
            Health = p.Health.Value,
            MaxHealth = p.MaxHealth.Value,
            PhysicalAttackBase = p.PhysicalAttackBase.Value,
            MagicAttackBase = p.MagicAttackBase.Value,
            PhysicalTakePercent = p.PhysicalTakePercent.Value,
            MagicTakePercent = p.MagicTakePercent.Value,
            CureIntensity = p.CureIntensity.Value,
            BaseSpeed = p.BaseSpeed.Value,
            Camps = [p.Camp.Value],
        };
    }
}
