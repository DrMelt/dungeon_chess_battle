using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Runtime.Shared.Combat;

/// <summary>
/// 单位状态副本：结算可读写的全部标量、读条、Buff、冷却与仇恨账本。
/// 按帧意图在帧末已作废，不入副本。
/// </summary>
public sealed record BattleUnitState(
    UnitId UnitId,
    float Health,
    Vector2 Position,
    Vector2 Direction,
    SkillKeyId SkillCasting,
    float SkillCastRemaining,
    UnitId FocusTarget,
    UnitCombatStateCopy Runtime);

/// <summary>
/// 战斗世界状态副本：世界时间态与按注册顺序排列的单位状态。
/// 恢复按注册顺序逐个写回并校验单位标识，单位集合与注册顺序必须与副本一致。
/// </summary>
public sealed record BattleWorldState(
    double BuffTickRemaining,
    float ElapsedTime,
    BattlePhase Phase,
    IReadOnlyList<BattleUnitState> Units);
