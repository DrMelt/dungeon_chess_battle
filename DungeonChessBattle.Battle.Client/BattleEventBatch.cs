using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Session.Shared;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 一帧战斗事件日志：本帧领域事件列表与其所在房间。
/// </summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="Events">本帧领域事件列表。</param>
public sealed record BattleEventBatch(RoomId RoomId, IReadOnlyList<IBattleEvent> Events);