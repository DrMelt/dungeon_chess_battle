using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Config.Shared.Content;

namespace DungeonChessBattle.Battle.Runtime.Shared.Combat;

/// <summary>
/// 以单位配置为蓝图装配战斗单位领域实体；数值、技能与仇恨规则取自配置。
/// 只做装配不查注册表，配置解析由调用方完成。无状态，服务端与回放预演可复用。
/// 装配只写状态与意图，单位驱动的意图源由宿主按配置声明另行登记给意图驱动。
/// </summary>
public static class BattleUnitFactory {
    /// <summary>按配置创建战斗单位，生命初始为满血，阵营与位置为装配期运行时参数。</summary>
    public static BattleUnit Create(
        UnitConfig config, UnitId unitId, IReadOnlyList<CampId> camps, Vector2 spawnPos) => new() {
            UnitId = unitId,
            UnitName = config.ConfigKey,
            Camps = camps,
            BaseConfig = config.BaseConfig,
            Skills = config.Skills,
            HateRule = config.HateRule,
            HateFactor = config.HateFactor,
            Health = config.BaseConfig.MaxHealth,
            Position = spawnPos,
        };
}
