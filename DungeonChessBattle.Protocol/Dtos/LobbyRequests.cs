namespace DungeonChessBattle.Protocol.Dtos;

/// <summary>
/// 客房招募板配置传输对象，create_room 的可选配置。
/// 房主 displayName 由服务端权威解析，不随此对象传输。
/// </summary>
/// <param name="DungeonKey">选中的副本键，服务端据此解析敌人生成配置。</param>
/// <param name="Description">招募板展示的房间描述。</param>
/// <param name="MaxPlayers">房间最大玩家数。</param>
public sealed record RoomConfigDto(
    string DungeonKey,
    string Description,
    int MaxPlayers);

/// <summary>创建房间请求，房间 ID 由服务端生成，经 LobbyResult.RoomId 返回。</summary>
/// <param name="PlayerId">房主玩家 ID。</param>
/// <param name="PlayerName">房主显示名。</param>
/// <param name="RoomPassword">房间密码；空表示无密码。</param>
/// <param name="Config">招募板配置；空表示使用默认值。</param>
/// <param name="ServerPassword">服务器密码；空表示无密码模式。</param>
public sealed record CreateRoomRequest(
    string PlayerId,
    string PlayerName,
    string? RoomPassword,
    RoomConfigDto? Config,
    string? ServerPassword);

/// <summary>加入房间请求。</summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="PlayerId">玩家 ID。</param>
/// <param name="PlayerName">玩家显示名。</param>
/// <param name="RoomPassword">房间密码；空表示无密码。</param>
/// <param name="ServerPassword">服务器密码；空表示无密码模式。</param>
public sealed record JoinRoomRequest(
    string RoomId,
    string PlayerId,
    string PlayerName,
    string? RoomPassword,
    string? ServerPassword);

/// <summary>准备阶段：添加单位请求。房间与阵营选项由服务端解析。</summary>
/// <param name="UnitConfigKey">单位配置键，与 UnitConfig.ConfigKey 一致。</param>
/// <param name="CampOptionKey">副本配置的玩家阵营选项键，服务端据此解析实际阵营。</param>
public sealed record PrepareAddUnitRequest(string UnitConfigKey, string CampOptionKey);

/// <summary>准备阶段：移除单位请求。房间由服务端从连接归属反查。</summary>
/// <param name="UnitConfigKey">单位配置键，与 UnitConfig.ConfigKey 一致。</param>
public sealed record PrepareRemoveUnitRequest(string UnitConfigKey);

/// <summary>准备阶段：设置是否已准备请求，仅非房主。房间与玩家名由服务端从连接归属反查。</summary>
/// <param name="Ready">是否已准备。</param>
public sealed record PrepareReadyStateRequest(bool Ready);

/// <summary>重连房间请求。</summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="PlayerId">玩家 ID。</param>
/// <param name="PlayerName">玩家显示名。</param>
/// <param name="RoomPassword">房间密码。</param>
/// <param name="ServerPassword">服务器密码。</param>
public sealed record ReconnectRoomRequest(
    string RoomId,
    string PlayerId,
    string PlayerName,
    string? RoomPassword,
    string? ServerPassword);
