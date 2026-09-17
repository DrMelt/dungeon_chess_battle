using DungeonChessBattle.Session.Shared;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 本地内容与服务端不一致的事实：本地缺服务端引用的副本或单位，本端已放弃战斗世界。
/// </summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="Reason">不一致原因。</param>
public sealed record BattleContentMismatch(RoomId RoomId, string Reason);