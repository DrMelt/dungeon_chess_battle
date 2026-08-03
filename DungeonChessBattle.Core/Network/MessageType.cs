namespace DungeonChessBattle.Core.Network;

/// <summary>
/// 客户端与服务端之间 JSON 消息的类型标识常量。
/// 服务端和客户端共同引用，消除硬编码字符串。
/// </summary>
public static class MessageType {
    // ── 请求类型 ──
    public const string CreateRoom = "create_room";
    public const string JoinRoom = "join_room";
    public const string ListRooms = "list_rooms";
    public const string StartBattle = "start_battle";
    public const string EndBattle = "end_battle";
    public const string CreateUnit = "create_unit";

    // ── 准备阶段（大厅 JSON 协议）──
    public const string PrepareAddUnit = "prepare_add_unit";
    public const string PrepareRemoveUnit = "prepare_remove_unit";
    public const string PrepareStartBattle = "prepare_start_battle";
    public const string PrepareUnitList = "prepare_unit_list";

    // ── 响应类型 ──
    public const string CreateRoomResponse = "create_room_response";
    public const string JoinRoomResponse = "join_room_response";
    public const string ListRoomsResponse = "list_rooms_response";
    public const string PrepareStartBattleResponse = "prepare_start_battle_response";

    // ── 重连 ──
    public const string ReconnectRoom = "reconnect_room";
    public const string ReconnectRoomResponse = "reconnect_room_response";

    // ── 大厅重定向 ──
    public const string JoinRoomRedirect = "join_room_redirect";
}

/// <summary>
/// JSON 消息中的属性名常量。
/// </summary>
public static class MessageProperty {
    public const string Type = "type";
    public const string RoomId = "roomId";
    public const string Success = "success";
    public const string Error = "error";
    public const string UnitName = "unitName";
    public const string Camp = "camp";
    public const string Port = "port";
    public const string PlayerId = "playerId";
    public const string PlayerName = "playerName";
    public const string Password = "password";
    public const string ServerPassword = "serverPassword";

    // ── 招募板属性 ──
    public const string Title = "title";
    public const string Description = "description";
    public const string Category = "category";
    public const string HostName = "hostName";
    public const string MaxPlayers = "maxPlayers";
    public const string CurrentPlayers = "currentPlayers";
    public const string HasPassword = "hasPassword";
    public const string CreatedAt = "createdAt";
    public const string Status = "status";
    public const string Config = "config";
    public const string Rooms = "rooms";
}
