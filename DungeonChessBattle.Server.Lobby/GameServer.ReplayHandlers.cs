using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Server.Abstractions;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// GameServer 的回放查询与下载处理。
/// 身份经登录会话反查：连接建立时登记的登录名即服务端权威身份，
/// 回放按登录名解析的玩家记录主键归档；
/// 查询与下载以登录会话为身份依据，经 <see cref="IReplayStore"/> 读取归档，
/// 摘要投影为协议 DTO，下载字节流在归档时已编码。
/// </summary>
public partial class GameServer {
    /// <summary>查询当前登录玩家的回放摘要列表，最近在前；未登录时返回空列表。</summary>
    public Task<ReplayListResult> HandleGetReplaysAsync(string connectionId) {
        string? playerName = _stateStore.GetLoginPlayerName(connectionId);
        if (string.IsNullOrEmpty(playerName))
            return Task.FromResult(new ReplayListResult([]));

        string recordId = _stateStore.ResolvePlayerRecordId(playerName);
        var summaries = _replayStore.GetReplaysByPlayerId(recordId)
            .Select(ToSummary)
            .ToList();
        return Task.FromResult(new ReplayListResult(summaries));
    }

    /// <summary>按房间 ID 签发回放下载凭证；仅该回放参与者可获得，失败时返回错误描述。回放字节经 HTTP 下载端点凭凭证换取。</summary>
    public Task<ReplayDownloadResult> HandleDownloadReplayAsync(string connectionId, string roomId) {
        string? playerName = _stateStore.GetLoginPlayerName(connectionId);
        if (string.IsNullOrEmpty(playerName) || string.IsNullOrWhiteSpace(roomId))
            return Task.FromResult(new ReplayDownloadResult(string.Empty, false, Error: "Invalid request."));

        string recordId = _stateStore.ResolvePlayerRecordId(playerName);
        // 参与关系经回放摘要反查，非参与者不暴露回放存在性；反查命中即证明回放存在
        bool isParticipant = _replayStore.GetReplaysByPlayerId(recordId).Any(s => s.RoomId == roomId);
        if (!isParticipant)
            return Task.FromResult(new ReplayDownloadResult(roomId, false, Error: "Replay not found."));
        return Task.FromResult(new ReplayDownloadResult(roomId, true, DownloadTicket: _replayTicketStore.Issue(roomId)));
    }

    private static ReplaySummaryDto ToSummary(ReplaySummary summary) {
        return new ReplaySummaryDto(
            summary.RoomId,
            summary.DungeonKey,
            summary.StartUnixTime,
            summary.TickRate,
            [.. summary.Players.Select(p => new ReplayPlayerDto(p.PlayerRecordId, p.PlayerName, p.UnitConfigKey))]);
    }
}
