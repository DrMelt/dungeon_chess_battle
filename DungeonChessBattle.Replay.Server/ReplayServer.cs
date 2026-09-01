using DungeonChessBattle.Replay.Protocol.Dtos;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Server.Abstractions;

namespace DungeonChessBattle.Replay.Server;

/// <summary>
/// 回放服务端业务：按参与者取摘要列表、取归档字节流。
/// 入参一律是玩家记录主键——会话凭证的解析在端点完成，本类不认识凭证、连接与登录。
/// 可见性边界：非参与者与不存在的回放同回 false，不暴露回放存在性。
/// 摘要不另存，从归档的元数据块现读：列表展示的就是重放端将来读到的那一份元数据。
/// </summary>
/// <param name="replayStore">回放归档存储。</param>
internal sealed class ReplayServer(IReplayStore replayStore) {
    /// <summary>取该主键参与过的回放摘要列表，最近在前；元数据读不出的归档不列入。</summary>
    public ReplayListResult GetReplays(string recordId) {
        var replays = new List<ReplaySummaryDto>();
        foreach (string roomId in replayStore.GetRoomIdsByPlayer(recordId)) {
            if (!replayStore.TryGetArchive(roomId, out byte[] archive))
                continue;
            var meta = ReplayArchive.TryReadMeta(archive);
            if (meta.Status == ReplayArchiveStatus.Ok && meta.Meta is { } info)
                replays.Add(ReplaySummaryDto.From(info));
        }

        return new ReplayListResult(replays);
    }

    /// <summary>取该主键参与过的回放归档字节流；房间 ID 非法、非参与者或归档不存在时返回 false。</summary>
    public bool TryGetArchive(string recordId, string roomId, out byte[] archive) {
        archive = [];
        if (string.IsNullOrWhiteSpace(roomId))
            return false;

        // 参与关系经该玩家的房间 ID 索引校验：命中即证明回放存在且归属可查
        if (!replayStore.GetRoomIdsByPlayer(recordId).Any(id => id == roomId))
            return false;
        return replayStore.TryGetArchive(roomId, out archive);
    }
}
