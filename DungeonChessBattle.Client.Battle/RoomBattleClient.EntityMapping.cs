using DungeonChessBattle.Entities;
using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Logic.Movement;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// RoomBattleClient 的 LES 实体创建回调与本地 Pawn 查询工具。
/// 展示层直读 UnitPawn 的 SyncVar，不再维护客户端模型中转。
/// </summary>
public partial class RoomBattleClient {
    /// <summary>当前房间的副本键，来自服务端权威 IReadOnlyBattleRoom.DungeonKey 同步。</summary>
    public string? DungeonKey => _roomEntity?.DungeonKey;

    /// <summary>房间实体创建回调：缓存房间与当前房间 ID，订阅事件日志并日志经 IReadOnlyBattleRoom 读取投影状态。</summary>
    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        IReadOnlyBattleRoom room = entity;
        lock (_lock) {
            _roomEntity = room;
            _currentRoomId = room.RoomId;
        }
        entity.BattleEventsReceived += OnRoomBattleEvents;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room entity created: {RoomId}, phase={Phase}, startUnix={StartUnix}, dungeonKey={DungeonKey}",
                room.RoomId, room.CurrentPhase, room.BattleStartUnixTime, room.DungeonKey);
    }

    /// <summary>房间事件日志转发：带当前房间 ID 暴露给服务门面，供 UI 订阅瞬时表现。</summary>
    private void OnRoomBattleEvents(IReadOnlyList<IBattleEvent> events) {
        var roomId = _currentRoomId;
        if (roomId != null)
            BattleEventsReceived?.Invoke(roomId, events);
    }

    /// <summary>单位实体创建回调：缓存 Pawn 并订阅其事件。</summary>
    private void OnPawnEntityCreated(UnitPawn pawn) {
        // 注入移动管线，Logic 层 MovementResolver，含场景交互，并注册单位互斥。
        // 与服务端注入同一实现；场景在副本键同步后就绪，就绪前按自由移动回退；
        // 半径与位置延迟读取，规避实体构造时同步未完成的时序。
        pawn.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, pawn.BodyRadius.Value, GetOrCreateMovementScene(), pawn.Id);
        TryRegisterPawn(pawn);

        var unitName = pawn.UnitName.Value;
        lock (_lock) {
            _roomPawns.Add(pawn);
        }

        // 订阅 UnitPawn 事件
        pawn.HealthChanged += (u, newHealth, oldHealth) =>
            UnitHealthChanged?.Invoke(u.Id, newHealth, oldHealth);
        pawn.UnitDied += (u) =>
            UnitDied?.Invoke(u.Id);
        pawn.FocusTargetChanged += (u, target) =>
            UnitFocusTargetChanged?.Invoke(u.Id, target);

        // 触发 OnUnitCreated 事件，通知 UI 层
        var roomId = _currentRoomId;
        if (roomId != null)
            OnUnitCreated?.Invoke(roomId, pawn.Id, unitName, pawn.CampTags);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("UnitPawn entity created: {UnitName}, Camps={Camps}, Pos={Position}",
                unitName, string.Join(",", pawn.CampTags), pawn.Position.Value);
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

    /// <summary>按网络实体 ID 查找本房间的 Pawn 实体。</summary>
    public UnitPawn? FindPawnById(ushort netId) {
        lock (_lock) {
            return _roomPawns.Find(p => p.Id == netId);
        }
    }

    /// <summary>获取本房间全部 Pawn 实体的只读视图。返回内部列表引用，实体变更统一在主线程网络更新阶段发生，调用方仅允许枚举。</summary>
    public IReadOnlyList<UnitPawn> GetPawns() {
        lock (_lock) {
            return _roomPawns;
        }
    }

    /// <summary>本地玩家控制的单位 Pawn，控制器未就绪时返回 null。</summary>
    public UnitPawn? LocalUnitPawn => _localController?.ControlledEntity;
}
