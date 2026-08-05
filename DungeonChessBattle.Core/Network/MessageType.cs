namespace DungeonChessBattle.Core.Network;

/// <summary>
/// 客户端与服务端之间 JSON 消息的类型标识常量。
/// 服务端和客户端共同引用，消除硬编码字符串。
/// </summary>
public static class MessageType {
    /// <summary>创建房间请求。</summary>
    public const string CreateRoom = "create_room";

    /// <summary>加入房间请求。</summary>
    public const string JoinRoom = "join_room";

    /// <summary>获取房间列表请求。</summary>
    public const string ListRooms = "list_rooms";

    /// <summary>开始战斗请求。</summary>
    public const string StartBattle = "start_battle";

    /// <summary>结束战斗请求。</summary>
    public const string EndBattle = "end_battle";

    /// <summary>创建单位请求（战斗内）。</summary>
    public const string CreateUnit = "create_unit";

    /// <summary>准备阶段：添加单位。</summary>
    public const string PrepareAddUnit = "prepare_add_unit";

    /// <summary>准备阶段：移除单位。</summary>
    public const string PrepareRemoveUnit = "prepare_remove_unit";

    /// <summary>准备阶段：开始战斗请求。</summary>
    public const string PrepareStartBattle = "prepare_start_battle";

    /// <summary>准备阶段：广播房间单位列表。</summary>
    public const string PrepareUnitList = "prepare_unit_list";

    /// <summary>创建房间响应。</summary>
    public const string CreateRoomResponse = "create_room_response";

    /// <summary>加入房间响应。</summary>
    public const string JoinRoomResponse = "join_room_response";

    /// <summary>房间列表响应。</summary>
    public const string ListRoomsResponse = "list_rooms_response";

    /// <summary>准备阶段开始战斗响应。</summary>
    public const string PrepareStartBattleResponse = "prepare_start_battle_response";

    /// <summary>重连房间请求。</summary>
    public const string ReconnectRoom = "reconnect_room";

    /// <summary>重连房间响应。</summary>
    public const string ReconnectRoomResponse = "reconnect_room_response";

    /// <summary>加入房间重定向（跳转到房间战斗端口）。</summary>
    public const string JoinRoomRedirect = "join_room_redirect";
}

/// <summary>
/// JSON 消息中的属性名常量。
/// </summary>
public static class MessageProperty {
    /// <summary>消息类型。</summary>
    public const string Type = "type";

    /// <summary>房间 ID。</summary>
    public const string RoomId = "roomId";

    /// <summary>操作是否成功。</summary>
    public const string Success = "success";

    /// <summary>错误信息。</summary>
    public const string Error = "error";

    /// <summary>单位名称。</summary>
    public const string UnitName = "unitName";

    /// <summary>阵营。</summary>
    public const string Camp = "camp";

    /// <summary>端口。</summary>
    public const string Port = "port";

    /// <summary>玩家 ID。</summary>
    public const string PlayerId = "playerId";

    /// <summary>玩家名称。</summary>
    public const string PlayerName = "playerName";

    /// <summary>房间密码。</summary>
    public const string Password = "password";

    /// <summary>服务器密码。</summary>
    public const string ServerPassword = "serverPassword";

    /// <summary>房间标题。</summary>
    public const string Title = "title";

    /// <summary>房间描述。</summary>
    public const string Description = "description";

    /// <summary>房间分类。</summary>
    public const string Category = "category";

    /// <summary>房主名称。</summary>
    public const string HostName = "hostName";

    /// <summary>最大玩家数。</summary>
    public const string MaxPlayers = "maxPlayers";

    /// <summary>当前玩家数。</summary>
    public const string CurrentPlayers = "currentPlayers";

    /// <summary>是否有密码。</summary>
    public const string HasPassword = "hasPassword";

    /// <summary>创建时间。</summary>
    public const string CreatedAt = "createdAt";

    /// <summary>房间状态。</summary>
    public const string Status = "status";

    /// <summary>房间配置对象。</summary>
    public const string Config = "config";

    /// <summary>房间列表。</summary>
    public const string Rooms = "rooms";
}
