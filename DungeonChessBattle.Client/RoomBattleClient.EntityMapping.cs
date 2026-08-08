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

    /// <summary>玩家实体创建回调。</summary>
    private void OnPlayerEntityCreated(PlayerRoomEntity player) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Player entity created: {PlayerName}", player.PlayerName.Value);
    }

    /// <summary>
    /// 控制器实体构造回调：识别并缓存本地玩家的 UnitController（用于 SubmitPlayerInput）。
    /// 客户端单房间单连接（OnlyForOwner 分发），收到控制器实体即属主控制器；
    /// 不依赖 IsLocalControlled——该判断在构造回调时序上可能尚未同步完成，
    /// 误判会导致 _localController 恒为 null、输入被静默丢弃（Position 恒为 0）。
    /// </summary>
    private void OnUnitControllerCreated(UnitController controller) {
        var pawnName = controller.ControlledEntity?.UnitName.Value ?? "(null)";
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "[RoomBattleClient] UnitController constructed: PawnName={PawnName}, IsLocalControlled={IsLocalControlled}, AlreadyBound={AlreadyBound}",
                pawnName, controller.IsLocalControlled, _localController != null);

        if (_localController != null)
            return;

        _localController = controller;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Local UnitController bound: {PawnName}", pawnName);
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
        var model = new UnitModel {
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

        // 同步服务端位置（XZ 平面 → 世界坐标）
        var pos = p.Position.Value;
        model.SetPosition(new System.Numerics.Vector3(pos.X, 0f, pos.Y));
        return model;
    }
}
