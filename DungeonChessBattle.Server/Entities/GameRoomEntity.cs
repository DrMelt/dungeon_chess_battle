using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Server.Entities;

/// <summary>
/// 房间联网实体，关联 Logic 层的 GameRoom 并承载网络同步状态。
/// 后续接入 LiteEntitySystem 时继承 NetworkEntity 并标记同步字段。
/// </summary>
public class GameRoomEntity(GameRoom room) {
    public GameRoom Room {
        get;
    } = room;
    public List<PlayerEntity> Players { get; } = [];
}