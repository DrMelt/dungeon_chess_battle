using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Inputs;
using DungeonChessBattle.Battle.Server.Replay;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Replay.Shared;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// BattleRoomServer 的战斗输入回放录制：命令在提交输入门面的同一处落盘，
/// 仅房间线程调用，导出交房间销毁后的归档线程。数据模型与时间轴见 <see cref="BattleReplayRecorder"/>。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>战斗输入录制器，房间线程首帧初始化时创建，随房间释放。</summary>
    private BattleReplayRecorder? _replayRecorder;

    /// <summary>
    /// 导出回放内容供归档；房间线程已退出，帧轴与轨道稳定。
    /// 两项修订号在本层读取后供给：录制器只搬运内容版本而不解释它，Battle.Server 的读取点集中在此一处。
    /// 编码与归档归属由调用方决定，本类不碰存储。
    /// </summary>
    internal ReplayRecording? BuildReplayRecording() => _replayRecorder?.BuildRecording(
        GameConfigDB.DataRevision, BattleLogicRevision.Value);

    /// <summary>创建回放记录器：玩家表下标即记录里的玩家序号，网络 ID 到序号的反查收在录制器内。</summary>
    private void CreateReplayRecorder(IReadOnlyList<ReplayPlayerInfo> players) {
        _replayRecorder = new BattleReplayRecorder(RoomId, _dungeonKey,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), FramesPerSecond, players);
    }

    /// <summary>
    /// 录制全部单位初始态：按创建顺序遍历房间载体，玩家与敌人同表。
    /// 重放端据此重建世界，不再从副本配置的生成顺序推演实体 ID；谁是玩家不在此落盘，
    /// 由元数据玩家表的 NetId 判定。仅房间线程调用。
    /// </summary>
    private void RecordUnitInits() {
        if (_replayRecorder is null)
            return;

        var units = new List<ReplayUnitInit>(_roomPawns.Count);
        foreach (var pawn in _roomPawns) {
            var position = pawn.Position.Value;
            units.Add(new ReplayUnitInit(pawn.Id, pawn.UnitKeyName.Value, pawn.CampTags,
                position.X, position.Y));
        }

        _replayRecorder.SetUnits(units);
    }

    /// <summary>战斗世界进入结束态时把结束帧写进回放：结束之后的帧不在时间轴内。仅房间线程调用。</summary>
    private void RecordBattleEnd(BattleScene scene) {
        if (scene.IsFinished)
            _replayRecorder?.MarkEnd(EntityManager.Tick);
    }

    /// <summary>提交一条玩家命令并旁路记录，返回权威接管结论作为客户端回执。仅房间线程调用。</summary>
    private bool SubmitAndRecord(in PlayerCommand cmd) {
        bool accepted = _intentHub.Submit(cmd);
        _replayRecorder?.Record(EntityManager.Tick, cmd, accepted);

        // 移动命令逐 tick 到来，不记日志；请求类命令一条一行，与客户端回执对照
        if (cmd.Kind == PlayerCommandKind.Move)
            return accepted;
        if (accepted) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[RoomId: {RoomId}] Player command taken: {Command}.", RoomId, cmd);
        }
        else if (_logger.IsEnabled(LogLevel.Warning))
            _logger.LogWarning("[RoomId: {RoomId}] Player command rejected: {Command}.", RoomId, cmd);
        return accepted;
    }
}

