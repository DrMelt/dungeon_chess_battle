using System.Buffers;
using System.Text.Json;

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
    public static byte[] WriteCreateUnit(string roomId, string unitName, byte camp) {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buf);

        writer.WriteStartObject();
        writer.WriteString(TypeKey, MessageType.CreateUnit);
        writer.WriteString(RoomIdKey, roomId);
        writer.WriteString(UnitNameKey, unitName);
        writer.WriteNumber(CampKey, camp);
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
}
