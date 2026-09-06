using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 以单位配置为蓝图装配战斗单位领域实体；数值、技能、AI 与仇恨规则全部取自配置。
/// 只做装配不查注册表，配置解析由调用方完成。无状态，服务端与回放预演可复用。
/// </summary>
public static class BattleUnitFactory {
    /// <summary>按配置创建战斗单位，生命初始为满血，阵营与位置为装配期运行时参数。</summary>
    public static BattleUnit Create(UnitConfig config, UnitId unitId, IReadOnlyList<string> camps, Vector2 spawnPos) => new() {
        UnitId = unitId,
        UnitName = config.ConfigKey,
        Camps = camps,
        BaseConfig = config.BaseConfig,
        Skills = config.Skills,
        Intelligence = config.Intelligence,
        HateRule = config.HateRule,
        HateFactor = config.HateFactor,
        Health = config.BaseConfig.MaxHealth,
        Position = spawnPos,
    };
}
