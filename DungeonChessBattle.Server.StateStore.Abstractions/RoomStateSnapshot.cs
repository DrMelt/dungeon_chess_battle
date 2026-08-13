namespace DungeonChessBattle.Server.StateStore.Abstractions;

/// <summary>
/// 房间准备状态快照，用于向房间内玩家广播。
/// </summary>
/// <param name="HostName">房主玩家名。</param>
/// <param name="DungeonName">副本名。</param>
/// <param name="DungeonKey">选中的副本键。</param>
/// <param name="Players">房间内玩家准备状态列表。</param>
public sealed record RoomStateSnapshot(
    string HostName,
    string DungeonName,
    string DungeonKey,
    IReadOnlyList<PlayerReadyState> Players);
