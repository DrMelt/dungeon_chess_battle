using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using LiteEntitySystem.Internal;

namespace DungeonChessBattle.Entities;

public class PlayerRoomEntity : EntityLogic
{
    public readonly SyncString PlayerName = new();
    public SyncVar<bool> IsReady;
    public SyncVar<byte> Camp;

    public PlayerRoomEntity(EntityParams entityParams) : base(entityParams) { }

    protected override void OnConstructed()
    {
        PlayerName.Value = "Player";
        IsReady.Value = false;
        Camp.Value = 0;
    }
}