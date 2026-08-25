namespace DungeonChessBattle.Lobby.Protocol.Dtos;

/// <summary>玩家回放列表结果。</summary>
/// <param name="Replays">回放摘要列表，最近在前。</param>
public sealed record ReplayListResult(IReadOnlyList<ReplaySummaryDto> Replays);

/// <summary>回放摘要条目。</summary>
/// <param name="RoomId">房间 ID，下载回放的主键。</param>
/// <param name="DungeonKey">副本键。</param>
/// <param name="StartUnixTime">战斗开始时间，Unix 秒，UTC。</param>
/// <param name="TickRate">逻辑 tick 频率。</param>
/// <param name="Players">参与玩家。</param>
public sealed record ReplaySummaryDto(
    string RoomId,
    string DungeonKey,
    long StartUnixTime,
    int TickRate,
    IReadOnlyList<ReplayPlayerDto> Players);

/// <summary>回放参与玩家条目。</summary>
/// <param name="PlayerRecordId">玩家记录主键，经服务端玩家记录注册表解析，与战斗内玩家 ID 无关。</param>
/// <param name="PlayerName">玩家名。</param>
/// <param name="UnitConfigKey">使用的单位配置键。</param>
public sealed record ReplayPlayerDto(string PlayerRecordId, string PlayerName, string UnitConfigKey);

/// <summary>回放下载结果。</summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="Success">是否成功。</param>
/// <param name="DownloadTicket">一次性下载凭证，成功时返回；经 HTTP 下载端点换取回放字节流。</param>
/// <param name="Error">失败原因；成功时为空。</param>
public sealed record ReplayDownloadResult(string RoomId, bool Success, string? DownloadTicket = null, string? Error = null);
