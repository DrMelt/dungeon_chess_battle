using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Battle.Logic.Movement;
using BuffView = DungeonChessBattle.Battle.Domain.Combat.BuffView;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// RoomBattleClient 的 LES 实体创建回调与本地 Pawn 查询工具。
/// 展示层直读 UnitPawn 的 SyncVar，不再维护客户端模型中转。
/// </summary>
public partial class RoomBattleClient {
    /// <summary>当前房间的副本键，来自服务端权威 BattleRoomEntity.DungeonKey 同步。</summary>
    public string DungeonKey => _roomEntity?.DungeonKey.Value ?? string.Empty;

    /// <summary>房间实体创建回调：缓存房间与当前房间 ID。</summary>
    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        lock (_lock) {
            _roomEntity = entity;
            _currentRoomId = entity.RoomId.Value;

            // 回填服务端权威创建时间，>0 才覆盖，规避 OnConstructed 默认 0 的竞态时序
            if (entity.CreatedUnixTime.Value > 0) {
                _roomCreatedUnix = (long)entity.CreatedUnixTime.Value;
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room entity created: {RoomId}", entity.RoomId.Value);
    }

    /// <summary>单位实体创建回调：缓存 Pawn 并订阅其事件。</summary>
    private void OnPawnEntityCreated(UnitPawn pawn) {
        // 注入移动管线，Logic 层 MovementResolver，含场景交互。
        // 与服务端注入同一实现；延迟读 BodyRadius.Value，规避实体构造时同步未完成的时序。
        pawn.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, pawn.BodyRadius.Value, OpenMovementScene.Instance);

        var unitName = pawn.UnitName.Value;
        lock (_lock) {
            _roomPawns.Add(pawn);
        }

        // 订阅 UnitPawn 事件
        pawn.HealthChanged += (u, newHealth, oldHealth) =>
            UnitHealthChanged?.Invoke(u.Id, newHealth, oldHealth);
        pawn.UnitDied += (u) =>
            UnitDied?.Invoke(u.Id);
        pawn.BuffAdded += (u, buff) => {
            var eventData = MapBuffData(buff);
            UnitBuffAdded?.Invoke(u.Id, eventData);
        };
        pawn.BuffRemoved += (u, buff) => {
            var eventData = MapBuffData(buff);
            UnitBuffRemoved?.Invoke(u.Id, eventData);
        };
        pawn.FocusTargetChanged += (u, target) =>
            UnitFocusTargetChanged?.Invoke(u.Id, target);

        // 触发 OnUnitCreated 事件，通知 UI 层
        var roomId = _currentRoomId;
        if (roomId != null)
            OnUnitCreated?.Invoke(roomId, pawn.Id, unitName, pawn.Camp.Value);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("UnitPawn entity created: {UnitName}, Camp={Camp}, Pos={Position}",
                unitName, pawn.Camp.Value, pawn.Position.Value);
    }

    /// <summary>玩家实体创建回调。</summary>
    private void OnPlayerEntityCreated(PlayerRoomEntity player) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player entity created: {PlayerName}", player.PlayerName.Value);
    }

    /// <summary>
    /// 控制器实体构造回调：识别并缓存本地玩家的 UnitController，用于 SubmitPlayerInput。
    /// 客户端单房间单连接，OnlyForOwner 分发，收到控制器实体即属主控制器；
    /// 不依赖 IsLocalControlled——该判断在构造回调时序上可能尚未同步完成，
    /// 误判会导致 _localController 恒为 null、输入被静默丢弃，Position 恒为 0。
    /// </summary>
    private void OnUnitControllerCreated(UnitController controller) {
        var pawnName = controller.ControlledEntity?.UnitName.Value ?? "(null)";
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "UnitController constructed: PawnName={PawnName}, IsLocalControlled={IsLocalControlled}, AlreadyBound={AlreadyBound}",
                pawnName, controller.IsLocalControlled, _localController != null);

        if (_localController != null)
            return;

        _localController = controller;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Local UnitController bound: {PawnName}", pawnName);
    }

    /// <summary>将同步 Buff 数据映射为 UI 事件使用的展示视图。</summary>
    private static BuffView MapBuffData(SyncBuffData buff) => new() {
        BuffTypeId = buff.BuffTypeId,
        Remaining = buff.Remaining,
        StackCount = buff.StackCount,
        DamageType = buff.DamageType,
    };

    /// <summary>按网络实体 ID 查找本房间的 Pawn 实体。</summary>
    public UnitPawn? FindPawnById(ushort netId) {
        lock (_lock) {
            return _roomPawns.Find(p => p.Id == netId);
        }
    }

    /// <summary>获取本房间全部 Pawn 实体的只读快照，展示层枚举数据源。</summary>
    public IReadOnlyList<UnitPawn> GetPawns() {
        lock (_lock) {
            return [.. _roomPawns];
        }
    }

    /// <summary>本地玩家控制的单位 Pawn，控制器未就绪时返回 null。</summary>
    public UnitPawn? LocalUnitPawn => _localController?.ControlledEntity;
}
