using DungeonChessBattle.Battle.Shared.Inputs;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Battle.Server.Replay;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// BattleRoomServer 的战斗输入回放录制：命令在提交输入门面的同一处落盘，
/// 仅房间线程调用，快照供外部线程安全读取。数据模型与时间轴见 <see cref="BattleReplayRecorder"/>。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>战斗输入录制器，房间线程首帧初始化时创建，随房间释放。</summary>
    private BattleReplayRecorder? _replayRecorder;

    /// <summary>回放记录只读快照；房间运行中为实时数据，停止后为最终数据。未来回放消费端经此读取。</summary>
    internal ReplayRecordSnapshot? ReplaySnapshot => _replayRecorder?.GetSnapshot();

    /// <summary>创建回放记录器：玩家表下标即记录里的玩家序号，网络 ID 到序号的反查收在录制器内。</summary>
    private void CreateReplayRecorder(IReadOnlyList<ReplayPlayerInfo> players) {
        _replayRecorder = new BattleReplayRecorder(RoomId, _dungeonKey,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), FramesPerSecond, players);
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

