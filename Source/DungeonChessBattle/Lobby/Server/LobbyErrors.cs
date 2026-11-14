using DungeonChessBattle.Session.Shared;
using ErrorOr;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// 大厅业务的可预期失败：房间持久化的副本键缺失或指向已消失的副本。
/// 描述面向日志，自带房间 ID 与副本键。
/// </summary>
public static class LobbyErrors {
    /// <summary>房间快照没有副本键。</summary>
    public static Error RoomDungeonKeyMissing(RoomId roomId) => Error.Validation(
        code: "Lobby.Room.DungeonKeyMissing", description: $"房间 '{roomId}' 未记录副本键");

    /// <summary>房间记录的副本键未在内容注册表中注册。</summary>
    public static Error RoomDungeonKeyUnknown(RoomId roomId, string dungeonKey) => Error.NotFound(
        code: "Lobby.Room.DungeonKeyUnknown", description: $"房间 '{roomId}' 引用的副本未注册：{dungeonKey}");
}
