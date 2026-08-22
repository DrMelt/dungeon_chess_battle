namespace DungeonChessBattle.Server.Abstractions;

/// <summary>
/// 回放存储契约：战斗房间销毁时归档编码后的回放字节流与摘要，大厅据此查询玩家回放与下载。
/// 只暴露原语类型，不依赖回放数据契约所在实现层。
/// </summary>
public interface IReplayStore {
    /// <summary>归档一场战斗的回放，以房间 ID 为主键；重复归档幂等忽略。</summary>
    void Add(string roomId, ReplaySummary summary, byte[] data);

    /// <summary>查询玩家记录主键参与过的全部回放摘要，最近归档在前；PlayerId 即玩家记录主键。</summary>
    IReadOnlyList<ReplaySummary> GetReplaysByPlayerId(string recordId);

    /// <summary>按房间 ID 取回放字节流，不存在时返回 false。</summary>
    bool TryGetReplay(string roomId, out byte[] data);
}

/// <summary>回放摘要：战斗元数据与参与玩家，供大厅查询展示。</summary>
public sealed record ReplaySummary(
    string RoomId,
    string DungeonKey,
    long StartUnixTime,
    int TickRate,
    IReadOnlyList<ReplayPlayer> Players);

/// <summary>回放参与玩家条目。PlayerRecordId 为玩家记录主键，供按记录查询回放。</summary>
public sealed record ReplayPlayer(string PlayerRecordId, string PlayerName, string UnitConfigKey);
