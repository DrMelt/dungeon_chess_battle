namespace DungeonChessBattle.Core.Network;

/// <summary>
/// 客户端与服务端之间 JSON 消息的类型标识常量。
/// 服务端和客户端共同引用，消除硬编码字符串。
/// </summary>
public static class MessageType {
    // ── 请求类型 ──
    public const string CreateRoom = "create_room";
    public const string JoinRoom = "join_room";
    public const string StartBattle = "start_battle";
    public const string EndBattle = "end_battle";
    public const string CreateUnit = "create_unit";

    // ── 响应类型 ──
    public const string CreateRoomResponse = "create_room_response";
    public const string JoinRoomResponse = "join_room_response";

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
}
