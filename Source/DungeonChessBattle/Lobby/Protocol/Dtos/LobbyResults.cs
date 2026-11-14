namespace DungeonChessBattle.Lobby.Protocol.Dtos;

/// <summary>大厅请求的通用结果。</summary>
/// <param name="RoomId">相关房间 ID。</param>
/// <param name="Success">是否成功。</param>
/// <param name="Error">失败原因；成功时为空。</param>
/// <param name="Port">重定向端口，战斗启动、加入或重连时填充。</param>
public sealed record LobbyResult(string RoomId, bool Success, string? Error = null, int? Port = null);

/// <summary>招募板房间列表结果。</summary>
/// <param name="Rooms">房间列表。</param>
public sealed record RoomListResult(IReadOnlyList<RoomListing> Rooms);

/// <summary>准备阶段单位条目。</summary>
/// <param name="UnitConfigKey">单位配置键，与 UnitConfig.ConfigKey 一致。</param>
/// <param name="CampOptionKey">玩家阵营选项键，对应副本配置 PlayerCampOptions 中的选项。</param>
/// <param name="PlayerName">归属玩家名。</param>
public sealed record PrepareUnitDto(string UnitConfigKey, string CampOptionKey, string PlayerName);

/// <summary>玩家准备状态条目。</summary>
/// <param name="PlayerName">玩家名。</param>
/// <param name="Ready">是否已准备。</param>
public sealed record PlayerReadyDto(string PlayerName, bool Ready);

/// <summary>大厅重定向到房间端口。</summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="Port">房间战斗端口。</param>
public sealed record RoomRedirect(string RoomId, int Port);

/// <summary>登入大厅结果。</summary>
/// <param name="Success">是否成功。</param>
/// <param name="PlayerName">已登记的登录名；失败时为空。</param>
/// <param name="Error">失败原因；成功时为空。</param>
/// <param name="SessionToken">会话凭证，成功时签发，随登录会话作废；供服务端 HTTP 端点自证身份，
/// 是连接级概念，不属任何业务分组。</param>
public sealed record LoginResult(bool Success, string? PlayerName = null, string? Error = null,
    string? SessionToken = null);
