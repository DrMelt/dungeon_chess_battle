using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Logic.Movement;
using Microsoft.Extensions.Logging;
using BattlePhase = DungeonChessBattle.Battle.Shared.Combat.BattlePhase;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// RoomBattleClient 的 LES 实体创建回调。
/// 实体创建时同步落到本地状态镜像（RoomBattleStateMirror），展示层统一从镜像取数。
/// </summary>
public partial class RoomBattleClient {
    /// <summary>当前房间的副本键，来自服务端权威 BattleRoomEntity.DungeonKey 同步。</summary>
    public string? DungeonKey => _roomEntity?.DungeonKey.Value;

    /// <summary>房间实体创建回调：缓存房间与当前房间 ID，日志读取投影状态。</summary>
    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        lock (_lock) {
            _roomEntity = entity;
            _currentRoomId = entity.RoomId.Value;
        }
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room entity created: {RoomId}, phase={Phase}, startUnix={StartUnix}, dungeonKey={DungeonKey}",
                entity.RoomId.Value, (BattlePhase)entity.BattlePhase.Value,
                entity.BattleStartUnixTime.Value, entity.DungeonKey.Value);
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

        // 同步到本地状态镜像：单位创建时立即建骨架，其余状态由 UpdateAfterPollEvents 每帧刷新。
        // 先落点再发事件，保证事件触发时刻镜像已可查询（UnitShowManager 延迟建视图依赖此顺序）。
        _mirror.SyncFromPawn(pawn, EndTickToRemaining);

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
        _mirror.SetLocalUnit(controller.ControlledEntity?.Id ?? 0);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Local UnitController bound: {PawnName}", pawnName);
    }

}
