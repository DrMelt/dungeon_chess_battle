using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 房间内玩家实体。由 RoomEntityServer 在客户端连接时创建，
/// 通过 SyncVar 在网络间同步玩家身份和连接状态。
/// </summary>
public class PlayerRoomEntity : EntityLogic {
    public readonly SyncString PlayerName = new();
    /// <summary>公开可见的短标识（playerId 前 8 位），完整 playerId 仅存于服务端私有字典。</summary>
    public readonly SyncString DisplayId = new();
    public SyncVar<byte> PlayerState;
    public SyncVar<bool> IsReady;
    public SyncVar<byte> Camp;

    /// <summary>客户端事件：PlayerState 发生变化时触发（用于检测重连）。</summary>
    public event Action<PlayerRoomEntity, byte, byte>? PlayerStateChanged; // entity, newState, oldState

    public PlayerRoomEntity(EntityParams entityParams) : base(entityParams) { }

    protected override void OnConstructed() {
        PlayerName.Value = "Player";
        DisplayId.Value = string.Empty;
        PlayerState.Value = (byte)Core.Enums.PlayerConnectionState.Connected;
        IsReady.Value = false;
        Camp.Value = 0;
    }
}
