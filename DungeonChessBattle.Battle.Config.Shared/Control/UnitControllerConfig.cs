using DungeonChessBattle.Battle.Shared.Control;

namespace DungeonChessBattle.Battle.Config.Shared.Control;

/// <summary>
/// 单位控制者配置：单位自治驱动所用的决策算法，单一封闭类型。
/// 玩家驱动单位不配本项；本项只表达驱动方式，玩家可选性由注册动作表达，不由配置字段表达。
/// </summary>
/// <param name="Decision">决策算法，须无状态、可多单位共享。</param>
public sealed record UnitControllerConfig(IUnitDecision Decision);
