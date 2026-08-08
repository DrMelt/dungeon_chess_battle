using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// RoomBattleClient 的 LES 实体创建回调与本地 Pawn 查询工具。
/// 展示层直读 UnitPawn（SyncVar），不再维护客户端 UnitModel 中转。
/// </summary>
public partial class RoomBattleClient {
    /// <summary>房间实体创建回调：缓存房间与当前房间 ID。</summary>
    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        lock (_lock) {
            _roomEntity = entity;
            _currentRoomId = entity.RoomId.Value;
            _persistentRoom ??= new GameRoom(entity.RoomId.Value);

            // 回填服务端权威创建时间（>0 才覆盖，规避 OnConstructed 默认 0 的竞态时序）
            if (entity.CreatedUnixTime.Value > 0) {
                _persistentRoom.CreatedAt = DateTimeOffset
                    .FromUnixTimeSeconds((long)entity.CreatedUnixTime.Value).UtcDateTime;
            }
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
    public UnitPawn? FindPawnByName(string unitName) {
        lock (_lock) {
            return _roomPawns.Find(p => p.UnitName.Value == unitName);
        }
    }

    /// <summary>获取本房间全部 Pawn 实体的只读快照（展示层枚举数据源）。</summary>
    public System.Collections.Generic.IReadOnlyList<UnitPawn> GetPawns() {
        lock (_lock) {
            return [.. _roomPawns];
        }
    }
}
