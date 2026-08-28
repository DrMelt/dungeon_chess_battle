using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Replay.Protocol.Dtos;

namespace DungeonChessBattle.Replay.Server;

/// <summary>
/// 回放服务端业务：按参与者取摘要列表、取归档字节流。
/// 入参一律是玩家记录主键——会话凭证的解析在端点完成，本类不认识凭证、连接与登录。
/// 可见性边界：非参与者与不存在的回放同回 false，不暴露回放存在性。
/// 摘要从 <see cref="IReplayStore"/> 归档读取并投影为协议 DTO；字节流原样交给端点流式输出。
/// </summary>
/// <param name="replayStore">回放归档存储。</param>
internal sealed class ReplayServer(IReplayStore replayStore) {
    /// <summary>取该主键参与过的回放摘要列表，最近在前。</summary>
    public ReplayListResult GetReplays(string recordId) =>
        new([.. replayStore.GetReplaysByPlayerId(recordId).Select(ToSummary)]);

    /// <summary>取该主键参与过的回放归档字节流；房间 ID 非法、非参与者或归档不存在时返回 false。</summary>
    public bool TryGetArchive(string recordId, string roomId, out byte[] data) {
        data = [];
        if (string.IsNullOrWhiteSpace(roomId))
            return false;

        // 参与关系经该玩家的摘要索引校验：命中即证明回放存在且归属可查
        if (!replayStore.GetReplaysByPlayerId(recordId).Any(summary => summary.RoomId == roomId))
            return false;
        if (!replayStore.TryGetReplay(roomId, out byte[] stored))
            return false;

        data = stored;
        return true;
    }

    private static ReplaySummaryDto ToSummary(ReplaySummary summary) => new(
        summary.RoomId,
        summary.DungeonKey,
        summary.StartUnixTime,
        summary.TickRate,
        summary.DataVersion,
        [.. summary.Players.Select(p => new ReplayPlayerDto(p.PlayerRecordId, p.PlayerName, p.UnitConfigKey))]);
}
