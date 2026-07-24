namespace DungeonChessBattle.Server.Entities;

/// <summary>
/// 玩家联网实体，承载连接状态与所属阵营。
/// 后续接入 LiteEntitySystem 时继承 NetworkEntity 并标记同步字段。
/// </summary>
public class PlayerEntity
{
    public int PeerId { get; }
    public string PlayerName { get; set; } = string.Empty;
    public bool IsReady { get; set; }

    public PlayerEntity(int peerId)
    {
        PeerId = peerId;
    }
}