namespace DungeonChessBattle.Client.Battle.Diagnostics;

/// <summary>
/// 传输层通用指标，房间链路，当前延迟与每秒收发统计。
/// 出站为应用层每次发送调用的近似值，1 包加字节长度，非原始 UDP 报文。
/// </summary>
public readonly record struct TransportMetrics(
    int LatencyMs,
    int PacketsInPerSecond, long BytesInPerSecond,
    int PacketsOutPerSecond, long BytesOutPerSecond);

/// <summary>
/// LES 实体同步指标：仅战斗/房间链路有意义，未进入战斗时为 null。
/// </summary>
public sealed record BattleEntityMetrics(
    ushort ServerTick, ushort Tick, ushort LastProcessedTick,
    int StoredCommands, ushort EntitiesCount, byte ServerInputBuffer,
    int LerpBufferCount, float LerpBufferTimeLength,
    float NetworkJitter, int PendingToRemoveEntities);

/// <summary>
/// 对外唯一网络状态快照契约。消费方，如后续调试 UI，只依赖本类型。
/// </summary>
public sealed record NetworkStatusSnapshot(
    bool IsConnected, string Host, int Port,
    TransportMetrics Transport, BattleEntityMetrics? Entity);
