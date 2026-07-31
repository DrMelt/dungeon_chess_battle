using System.Buffers;
using System.Text;
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
    /// 写入加入房间重定向响应：{ "type":"join_room_redirect", "roomId":..., "success":true, "port":... }
    /// </summary>
    public static byte[] WriteJoinRoomRedirect(string roomId, int roomPort) {
        return WriteResponse(MessageType.JoinRoomRedirect, roomId, true, port: roomPort);
    }
}
