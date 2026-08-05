using DungeonChessBattle.Core.Enums;
using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 房间内玩家实体。由 BattleRoomServer 在客户端连接时创建，
/// 通过 SyncVar 在网络间同步玩家身份和连接状态。
/// </summary>
public class PlayerRoomEntity : EntityLogic {
    /// <summary>玩家名称。</summary>
    public readonly SyncString PlayerName = new();

    /// <summary>公开可见的短标识（playerId 前 8 位），完整 playerId 仅存于服务端私有字典。</summary>
    public readonly SyncString DisplayId = new();

    /// <summary>连接状态（对应 PlayerConnectionState 枚举的 byte 值）。</summary>
    public SyncVar<byte> PlayerState;

    /// <summary>玩家是否已准备。</summary>
    public SyncVar<bool> IsReady;

    /// <summary>玩家阵营字符串标识（如 "Camp_A"、"Camp_B"）。</summary>
    public readonly SyncString Camp = new();

#pragma warning disable CS0067 // 预留事件接口：用于检测重连，当前版本暂未实现触发逻辑
    /// <summary>客户端事件：PlayerState 发生变化时触发（用于检测重连）。参数：实体、新状态、旧状态。</summary>
    public event Action<PlayerRoomEntity, byte, byte>? PlayerStateChanged;
#pragma warning restore CS0067

    /// <summary>
    /// 初始化玩家实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public PlayerRoomEntity(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// 实体构造完成回调：初始化玩家默认状态。
    /// </summary>
    protected override void OnConstructed() {
        PlayerName.Value = "Player";
        DisplayId.Value = string.Empty;
        PlayerState.Value = (byte)Core.Enums.PlayerConnectionState.Connected;
        IsReady.Value = false;
        Camp.Value = string.Empty;
    }
}
