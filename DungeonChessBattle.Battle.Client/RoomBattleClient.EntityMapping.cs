using DungeonChessBattle.Battle.Entities;
using Microsoft.Extensions.Logging;
using BattlePhase = DungeonChessBattle.Battle.Shared.Combat.BattlePhase;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// RoomBattleClient 的 LES 实体创建回调。
/// 实体创建时构建领域 BattleUnit，展示层经 IBattleViewSource 取数。
/// </summary>
public partial class RoomBattleClient {
    /// <summary>当前房间的副本键，来自服务端权威 BattleRoomEntity.DungeonKey 同步。</summary>
    public string? DungeonKey => RoomState.DungeonKey;

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

    /// <summary>单位实体创建回调：构建本地领域单位注册。</summary>
    private void OnPawnEntityCreated(UnitPawn pawn) {
        var unitName = pawn.UnitKeyName.Value;

        // 只构建领域单位并注册；位移、生命等状态由 ClientBattleLoop 每渲染帧从 SyncVar 回填。
        AddPawnUnit(pawn);

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
        var pawnName = controller.ControlledEntity?.UnitKeyName.Value ?? "(null)";
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "UnitController constructed: PawnName={PawnName}, IsLocalControlled={IsLocalControlled}, AlreadyBound={AlreadyBound}",
                pawnName, controller.IsLocalControlled, _localController != null);

        if (_localController != null)
            return;

        _localController = controller;
        _localNetId = controller.ControlledEntity?.Id ?? 0;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Local UnitController bound: {PawnName}", pawnName);
    }

}
