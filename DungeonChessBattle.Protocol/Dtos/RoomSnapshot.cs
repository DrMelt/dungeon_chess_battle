using DungeonChessBattle.Protocol.Enums;

namespace DungeonChessBattle.Protocol.Dtos;

/// <summary>
/// 房间完整状态快照：服务端组装后单次下发给客户端的房间权威视图。
/// 合并了招募板静态配置与准备阶段动态数据，含准备状态与单位，
/// 客户端以它为准、无需自行组装或拼接。
/// </summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="Title">房间标题。</param>
/// <param name="Description">房间描述。</param>
/// <param name="MaxPlayers">房间最大玩家数。</param>
/// <param name="Status">房间状态，阶段。</param>
/// <param name="HostName">房主玩家名，服务端权威。</param>
/// <param name="DungeonName">副本名。</param>
/// <param name="CurrentPlayers">房间当前玩家数。</param>
/// <param name="Players">房间内玩家列表，包含玩家名与准备标志。</param>
/// <param name="Units">准备单位列表。</param>
public sealed record RoomSnapshot(
    string RoomId,
    string Title,
    string Description,
    int MaxPlayers,
    RoomStatus Status,
    string HostName,
    string DungeonName,
    int CurrentPlayers,
    IReadOnlyList<PlayerReadyDto> Players,
    IReadOnlyList<PrepareUnitDto> Units);
