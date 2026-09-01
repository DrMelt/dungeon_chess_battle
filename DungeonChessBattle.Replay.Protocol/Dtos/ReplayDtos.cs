using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Replay.Protocol.Dtos;

/// <summary>玩家回放列表结果。</summary>
/// <param name="Replays">回放摘要列表，最近在前。</param>
public sealed record ReplayListResult(IReadOnlyList<ReplaySummaryDto> Replays);

/// <summary>
/// 回放摘要条目：由归档字节流的元数据块投影，服务端不另存摘要。
/// 投影只有 <see cref="From"/> 一份——服务端列归档与客户端列本地副本走同一条路，字段不会随来源漂。
/// </summary>
/// <param name="RoomId">房间 ID，下载回放的主键。</param>
/// <param name="DungeonKey">副本键。</param>
/// <param name="StartUnixTime">战斗开始时间，Unix 秒，UTC。</param>
/// <param name="TickRate">逻辑 tick 频率。</param>
/// <param name="DurationTicks">回放覆盖的逻辑帧数，时长即 DurationTicks / TickRate。</param>
/// <param name="DataVersion">录制端内容数据修订号，与客户端不一致时回放不可播放。</param>
/// <param name="LogicVersion">录制端结算逻辑修订号，与客户端不一致时回放不可播放。</param>
/// <param name="Players">参与玩家。</param>
public sealed record ReplaySummaryDto(
    string RoomId,
    string DungeonKey,
    long StartUnixTime,
    int TickRate,
    int DurationTicks,
    string DataVersion,
    string LogicVersion,
    IReadOnlyList<ReplayPlayerDto> Players) {
    /// <summary>归档元数据 → 摘要条目：帧轴折成时长，玩家表丢掉不上线的 NetId。</summary>
    public static ReplaySummaryDto From(ReplayMeta meta) => new(
        meta.RoomId,
        meta.DungeonKey,
        meta.StartUnixTime,
        meta.TickRate,
        meta.DurationTicks,
        meta.DataVersion,
        meta.LogicVersion,
        [.. meta.Players.Select(static player => new ReplayPlayerDto(player.PlayerName, player.UnitConfigKey))]);
}

/// <summary>
/// 回放参与玩家条目。归档归属的玩家记录主键不在此列：它只活在服务端的参与者索引里，
/// 不出现在可下载的归档中。
/// </summary>
/// <param name="PlayerName">玩家名。</param>
/// <param name="UnitConfigKey">使用的单位配置键。</param>
public sealed record ReplayPlayerDto(string PlayerName, string UnitConfigKey);
