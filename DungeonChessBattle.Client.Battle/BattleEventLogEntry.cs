using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 战斗事件日志条目：领域事件与其客户端接收时刻（UTC Unix 毫秒）。
/// 纯数据值类型，由 BattleEventLogStore 保存，UI 只读消费。
/// </summary>
public readonly record struct BattleEventLogEntry(long ReceiveUnixMs, IBattleEvent Event);
