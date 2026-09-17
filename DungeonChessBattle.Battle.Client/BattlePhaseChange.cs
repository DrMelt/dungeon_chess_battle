using DungeonChessBattle.Session.Shared;
using BattlePhase = DungeonChessBattle.Battle.Shared.Combat.BattlePhase;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 战斗阶段的一次变化：变化后的阶段与其所在房间。
/// </summary>
/// <param name="RoomId">房间 ID。</param>
/// <param name="Phase">变化后的战斗阶段。</param>
public sealed record BattlePhaseChange(RoomId RoomId, BattlePhase Phase);