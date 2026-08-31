using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。房间级战斗状态展示载体。
/// 创建单位与开始战斗的请求已由大厅 SignalR 通道（AddPrepareUnit / StartBattle）承担，
/// 本实体承载房间级战斗状态的同步目标，由服务端状态同步器写入。
/// 同步字段全部以服务端写回为准，禁止在 OnConstructed 重置：
/// LES 1.2.2 客户端先应用初始同步状态再执行 OnConstructed，重置会让一次性写入字段
/// （BattleStartUnixTime、BattlePhase 等）在客户端丢失且不再回补。
/// 战斗事件日志经传输层可靠通道外送，本实体不再承载事件 RPC。
/// </summary>
public partial class BattleRoomEntity : EntityLogic {
    /// <summary>
    /// 初始化战斗房间实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }

    /// <summary>房间唯一 ID。</summary>
    public readonly SyncString RoomId = new();

    /// <summary>战斗阶段，对应 BattlePhase 枚举的 byte 值；结束由阶段推导。</summary>
    public SyncVar<ulong> BattlePhase;

    /// <summary>战斗开始时间，Unix 秒，UTC，服务端权威，取战斗世界构造时刻。</summary>
    public SyncVar<long> BattleStartUnixTime;

    /// <summary>房间选中的副本键，服务端权威，客户端据此呈现对应环境场景。</summary>
    public readonly SyncString DungeonKey = new();
}

