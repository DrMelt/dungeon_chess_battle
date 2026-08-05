using System.Buffers;
using System.Text.Json;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Core.Network;

/// <summary>
/// 基于 Utf8JsonWriter 的无反射 JSON 消息序列化工具。
/// 替代 JsonSerializer.Serialize(object) 的反射开销，适用于
/// NativeAOT 和 Trimming 场景。
/// </summary>
public static class MessageWriter {
    private static readonly JsonEncodedText TypeKey = JsonEncodedText.Encode("type");
    private static readonly JsonEncodedText RoomIdKey = JsonEncodedText.Encode("roomId");
    private static readonly JsonEncodedText SuccessKey = JsonEncodedText.Encode("success");
    private static readonly JsonEncodedText ErrorKey = JsonEncodedText.Encode("error");
    private static readonly JsonEncodedText UnitNameKey = JsonEncodedText.Encode("unitName");
    private static readonly JsonEncodedText CampKey = JsonEncodedText.Encode("camp");
    private static readonly JsonEncodedText PortKey = JsonEncodedText.Encode("port");
    private static readonly JsonEncodedText PlayerIdKey = JsonEncodedText.Encode("playerId");
    private static readonly JsonEncodedText PlayerNameKey = JsonEncodedText.Encode("playerName");
    private static readonly JsonEncodedText PasswordKey = JsonEncodedText.Encode("password");
    private static readonly JsonEncodedText ServerPasswordKey = JsonEncodedText.Encode("serverPassword");

    private static readonly JsonEncodedText ConfigKey = JsonEncodedText.Encode("config");
    private static readonly JsonEncodedText TitleKey = JsonEncodedText.Encode("title");
    private static readonly JsonEncodedText DungeonNameKey = JsonEncodedText.Encode("dungeonName");
    private static readonly JsonEncodedText DescriptionKey = JsonEncodedText.Encode("description");
    private static readonly JsonEncodedText CategoryKey = JsonEncodedText.Encode("category");
    private static readonly JsonEncodedText HostNameKey = JsonEncodedText.Encode("hostName");
    private static readonly JsonEncodedText MaxPlayersKey = JsonEncodedText.Encode("maxPlayers");
    private static readonly JsonEncodedText CurrentPlayersKey = JsonEncodedText.Encode("currentPlayers");
    private static readonly JsonEncodedText HasPasswordKey = JsonEncodedText.Encode("hasPassword");
    private static readonly JsonEncodedText CreatedAtKey = JsonEncodedText.Encode("createdAt");
    private static readonly JsonEncodedText StatusKey = JsonEncodedText.Encode("status");
    private static readonly JsonEncodedText RoomsKey = JsonEncodedText.Encode("rooms");
    private static readonly JsonEncodedText PlayersKey = JsonEncodedText.Encode("players");
    private static readonly JsonEncodedText ReadyKey = JsonEncodedText.Encode("ready");
    private static readonly JsonEncodedText UnitsKey = JsonEncodedText.Encode("units");

    /// <summary>
    /// 写入响应消息：{ "type":..., "roomId":..., "success":bool[, "error":...] }
    /// </summary>
    public static byte[] WriteResponse(string type, string? roomId, bool success, string? error = null, int? port = null) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, type);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteBoolean(SuccessKey, success);
        if (error != null)
            writer.WriteString(ErrorKey, error);
        if (port.HasValue)
            writer.WriteNumber(PortKey, port.Value);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入含 type + roomId 的请求消息：{ "type":..., "roomId":... }
    /// </summary>
    public static byte[] WriteRoomRequest(string type, string roomId) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, type);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入创建单位请求：{ "type":"create_unit", "roomId":..., "unitName":..., "camp":... }
    /// </summary>
    public static byte[] WriteCreateUnit(string roomId, string unitName, string camp) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.CreateUnit);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(UnitNameKey, unitName);
        writer.WriteString(CampKey, camp);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入含完整身份信息的房间请求：{ "type":..., "roomId":..., "playerName":..., "password"?..., "playerId":... }
    /// </summary>
    public static byte[] WriteRoomRequestFull(string type, string roomId, string playerName,
        string? roomPassword, string playerId, string? serverPassword = null) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, type);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(PlayerNameKey, playerName);
        writer.WriteString(PlayerIdKey, playerId);
        if (!string.IsNullOrEmpty(roomPassword))
            writer.WriteString(PasswordKey, roomPassword);
        if (!string.IsNullOrEmpty(serverPassword))
            writer.WriteString(ServerPasswordKey, serverPassword);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入重连请求：{ "type":"reconnect_room", "roomId":..., "playerId":..., "playerName":..., "password"?... }
    /// </summary>
    public static byte[] WriteReconnectRoom(string roomId, string playerId, string playerName,
        string? roomPassword = null, string? serverPassword = null) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.ReconnectRoom);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(PlayerIdKey, playerId);
        writer.WriteString(PlayerNameKey, playerName);
        if (!string.IsNullOrEmpty(roomPassword))
            writer.WriteString(PasswordKey, roomPassword);
        if (!string.IsNullOrEmpty(serverPassword))
            writer.WriteString(ServerPasswordKey, serverPassword);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入重连响应：{ "type":"reconnect_room_response", "roomId":..., "success":bool[, "error":..., "port":..., "playerId":...] }
    /// </summary>
    public static byte[] WriteReconnectRoomResponse(string? roomId, bool success, string? error = null,
        int? port = null, string? playerId = null) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.ReconnectRoomResponse);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteBoolean(SuccessKey, success);
        if (error != null)
            writer.WriteString(ErrorKey, error);
        if (port.HasValue)
            writer.WriteNumber(PortKey, port.Value);
        if (playerId != null)
            writer.WriteString(PlayerIdKey, playerId);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入加入房间重定向响应（含 playerId）：{ "type":"join_room_redirect", "roomId":..., "success":true, "port":..., "playerId":... }
    /// </summary>
    public static byte[] WriteJoinRoomRedirectWithPlayerId(string roomId, int roomPort, string playerId) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.JoinRoomRedirect);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteBoolean(SuccessKey, true);
        writer.WriteNumber(PortKey, roomPort);
        writer.WriteString(PlayerIdKey, playerId);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入加入房间重定向响应（不含 playerId，向后兼容）：{ "type":"join_room_redirect", "roomId":..., "success":true, "port":... }
    /// </summary>
    public static byte[] WriteJoinRoomRedirect(string roomId, int roomPort) {
        return WriteResponse(MessageType.JoinRoomRedirect, roomId, true, port: roomPort);
    }

    /// <summary>
    /// 写入创建房间请求（含招募板配置）：
    /// { "type":"create_room", "roomId":..., "playerName":..., "playerId":..., "password"?:...,
    ///   "config":{ "title":..., "description":..., "category":..., "hostName":..., "maxPlayers":... } }
    /// </summary>
    public static byte[] WriteCreateRoomRequest(string roomId, string playerName, string playerId,
        string? roomPassword, GameRoom config, string? serverPassword = null) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.CreateRoom);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(PlayerNameKey, playerName);
        writer.WriteString(PlayerIdKey, playerId);
        if (!string.IsNullOrEmpty(roomPassword))
            writer.WriteString(PasswordKey, roomPassword);
        if (!string.IsNullOrEmpty(serverPassword))
            writer.WriteString(ServerPasswordKey, serverPassword);

        // config
        writer.WriteStartObject(ConfigKey);
        writer.WriteString(TitleKey, config.Title);
        writer.WriteString(DungeonNameKey, config.DungeonName);
        writer.WriteString(DescriptionKey, config.Description);
        writer.WriteNumber(CategoryKey, (byte)config.Category);
        writer.WriteString(HostNameKey, config.HostName);
        writer.WriteNumber(MaxPlayersKey, config.MaxPlayers);
        writer.WriteEndObject();

        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入房间列表响应：
    /// { "type":"list_rooms_response", "rooms":[...] }
    /// </summary>
    public static byte[] WriteListRoomsResponse(IEnumerable<RoomListing> rooms) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.ListRoomsResponse);

        writer.WriteStartArray(RoomsKey);
        foreach (var room in rooms) {
            writer.WriteStartObject();
            writer.WriteString(RoomIdKey, room.RoomId);
            writer.WriteString(TitleKey, room.Title);
            writer.WriteString(DungeonNameKey, room.DungeonName);
            writer.WriteNumber(CategoryKey, (byte)room.Category);
            writer.WriteString(HostNameKey, room.HostName);
            writer.WriteNumber(CurrentPlayersKey, room.CurrentPlayers);
            writer.WriteNumber(MaxPlayersKey, room.MaxPlayers);
            writer.WriteBoolean(HasPasswordKey, room.HasPassword);
            writer.WriteNumber(StatusKey, (byte)room.Status);
            writer.WriteString(CreatedAtKey, room.CreatedAt.ToString("o"));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入添加单位请求：{ "type":"prepare_add_unit", "roomId":..., "unitName":..., "camp":... }
    /// </summary>
    public static byte[] WritePrepareAddUnit(string roomId, string unitName, string camp) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.PrepareAddUnit);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(UnitNameKey, unitName);
        writer.WriteString(CampKey, camp);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入移除单位请求：{ "type":"prepare_remove_unit", "roomId":..., "unitName":... }
    /// </summary>
    public static byte[] WritePrepareRemoveUnit(string roomId, string unitName) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.PrepareRemoveUnit);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(UnitNameKey, unitName);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入开始战斗请求：{ "type":"prepare_start_battle", "roomId":..., "playerName":... }
    /// </summary>
    public static byte[] WritePrepareStartBattle(string roomId, string playerName) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.PrepareStartBattle);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(PlayerNameKey, playerName);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入准备阶段开始战斗响应（含重定向端口）：
    /// { "type":"prepare_start_battle_response", "roomId":..., "success":true, "port":... }
    /// </summary>
    public static byte[] WritePrepareStartBattleResponse(string roomId, int roomPort) {
        return WriteResponse(MessageType.PrepareStartBattleResponse, roomId, true, port: roomPort);
    }

    /// <summary>
    /// 写入准备阶段单位列表通知：
    /// { "type":"prepare_unit_list", "roomId":..., "units":[{"unitName":..., "camp":..., "playerName":...}, ...] }
    /// </summary>
    public static byte[] WritePrepareUnitList(string roomId,
        IEnumerable<(string UnitName, string Camp, string PlayerName)> units) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.PrepareUnitList);
        writer.WriteString(RoomIdKey, roomId);

        writer.WriteStartArray(UnitsKey);
        foreach (var (unitName, camp, playerName) in units) {
            writer.WriteStartObject();
            writer.WriteString(UnitNameKey, unitName);
            writer.WriteString(CampKey, camp);
            writer.WriteString(PlayerNameKey, playerName);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入非房主准备请求：{ "type":"prepare_ready", "roomId":..., "playerName":... }
    /// </summary>
    public static byte[] WritePrepareReady(string roomId, string playerName) {
        return WritePrepareReadyState(MessageType.PrepareReady, roomId, playerName);
    }

    /// <summary>
    /// 写入非房主取消准备请求：{ "type":"prepare_unready", "roomId":..., "playerName":... }
    /// </summary>
    public static byte[] WritePrepareUnready(string roomId, string playerName) {
        return WritePrepareReadyState(MessageType.PrepareUnready, roomId, playerName);
    }

    /// <summary>写入准备/取消准备请求的公共实现。</summary>
    private static byte[] WritePrepareReadyState(string type, string roomId, string playerName) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, type);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(PlayerNameKey, playerName);
        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入房间准备状态广播：
    /// { "type":"prepare_room_state", "roomId":..., "hostName":..., "dungeonName":..., "players":[{"playerName":..., "ready":bool}, ...] }
    /// </summary>
    public static byte[] WritePrepareRoomState(string roomId, string hostName, string dungeonName,
        IEnumerable<(string PlayerName, bool Ready)> players) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.PrepareRoomState);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(HostNameKey, hostName);
        writer.WriteString(DungeonNameKey, dungeonName);

        writer.WriteStartArray(PlayersKey);
        foreach (var (playerName, ready) in players) {
            writer.WriteStartObject();
            writer.WriteString(PlayerNameKey, playerName);
            writer.WriteBoolean(ReadyKey, ready);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();

        writer.Flush();
        return buf.WrittenSpan.ToArray();
    }
}
