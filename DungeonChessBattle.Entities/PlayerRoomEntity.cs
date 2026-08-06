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
    /// 实体构造完成回调。
    /// ⚠ LiteEntitySystem 1.2.2 语义：OnConstructed 在 AddEntity(initAction) 之后执行，
    /// 会覆盖服务端注入值。此处仅保留纯内部默认状态；
    /// 运行时注入字段（PlayerName/Camp/PlayerState/IsReady 等）禁止在此赋默认值。
    /// </summary>
    protected override void OnConstructed() {
        // 所有字段均由服务端 AddEntity(initAction) 注入，此处不再设置任何默认值，
        // 避免覆盖注入值（参见 UnitPawn.OnConstructed 注释）。
    }
}
