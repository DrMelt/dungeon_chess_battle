using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.Requests;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Server.Battle.Replay;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// BattleRoomServer 的战斗输入回放录制。
/// 记录在既有输入消费点旁路挂接，不改变权威校验与战斗逻辑；
/// 仅房间线程调用记录，快照供外部线程安全读取。
/// 数据模型与时间轴见 <see cref="BattleReplayRecorder"/>。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>战斗输入录制器，房间线程首帧初始化时创建，随房间释放。</summary>
    private BattleReplayRecorder? _replayRecorder;

    /// <summary>unitNetId 到玩家表序号的索引，敌人与非玩家单位不在其中。</summary>
    private readonly Dictionary<ushort, byte> _playerIndexByNetId = [];

    /// <summary>录制满告警只触发一次。</summary>
    private bool _replayFullWarned;

    /// <summary>回放记录只读快照；房间运行中为实时数据，停止后为最终数据。未来回放消费端经此读取。</summary>
    internal ReplayRecordSnapshot? ReplaySnapshot => _replayRecorder?.GetSnapshot();

    /// <summary>创建回放记录器，玩家表序号与 <see cref="_playerIndexByNetId"/> 一致。仅房间线程调用。</summary>
    private void CreateReplayRecorder(IReadOnlyList<ReplayPlayerInfo> players) {
        _replayRecorder = new BattleReplayRecorder(RoomId, _dungeonKey,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), FramesPerSecond, players);
    }

    /// <summary>记录玩家单位移动输入；非玩家单位或未启用时跳过。</summary>
    private void TryRecordMoveInput(UnitPawn pawn, UnitInputPacket input) {
        if (_replayRecorder is not { } recorder || !_playerIndexByNetId.TryGetValue(pawn.Id, out byte index))
            return;
        if (!recorder.RecordMoveInput(EntityManager.Tick, index, input.MoveX, input.MoveY))
            WarnReplayFull();
    }

    /// <summary>记录施法请求与接受结果；非玩家单位或未启用时跳过。</summary>
    private void TryRecordCastSkill(UnitPawn pawn, CastSkillRequest req, bool accepted) {
        if (_replayRecorder is not { } recorder || !_playerIndexByNetId.TryGetValue(pawn.Id, out byte index))
            return;
        if (!recorder.RecordCastSkill(EntityManager.Tick, index, req.SkillTypeId, req.TargetNetId,
                req.TargetPosX, req.TargetPosZ, accepted))
            WarnReplayFull();
    }

    /// <summary>记录聚焦目标请求与接受结果；非玩家单位或未启用时跳过。</summary>
    private void TryRecordFocusTarget(UnitPawn pawn, ushort targetNetId, bool accepted) {
        if (_replayRecorder is not { } recorder || !_playerIndexByNetId.TryGetValue(pawn.Id, out byte index))
            return;
        if (!recorder.RecordFocusTarget(EntityManager.Tick, index, targetNetId, accepted))
            WarnReplayFull();
    }

    private void WarnReplayFull() {
        if (_replayFullWarned)
            return;
        _replayFullWarned = true;
        _logger.LogWarning("[RoomId: {RoomId}] Replay recording stopped: entry limit reached.", RoomId);
    }
}
