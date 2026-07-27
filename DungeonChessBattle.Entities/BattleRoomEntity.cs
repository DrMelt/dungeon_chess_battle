using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。纯数据载体。
/// </summary>
public class BattleRoomEntity : EntityLogic {
    public readonly SyncString RoomId = new();
    public SyncVar<byte> BattlePhase;
    public SyncVar<ushort> CurrentRound;
    public SyncVar<byte> ActiveCamp;
    public SyncVar<bool> IsFinished;
    public SyncVar<byte> WinnerCamp;

    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }

    protected override void OnConstructed() {
        BattlePhase.Value = 0;
        CurrentRound.Value = 1;
        ActiveCamp.Value = 1;
        IsFinished.Value = false;
        WinnerCamp.Value = 0;
    }
}
