using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Movement;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.GameConfig.Models;
using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.GameConfig;

public sealed partial class ContentSetRegistry {
    private void BuildUnits(ModContentJson content, BehaviorCatalog catalog) {
        foreach (var dto in content.Units) {
            var unit = new UnitConfig {
                ConfigKey = dto.ConfigKey,
                IsPlayerSelectable = dto.IsPlayerSelectable,
                HateFactor = dto.HateFactor,
                BaseConfig = new UnitBaseConfig(
                    MaxHealth: dto.MaxHealth,
                    BodyRadius: dto.BodyRadius,
                    BaseSpeed: dto.BaseSpeed,
                    PhysicalAttackBase: dto.PhysicalAttackBase,
                    PhysicalTakePercent: dto.PhysicalTakePercent,
                    MagicAttackBase: dto.MagicAttackBase,
                    MagicTakePercent: dto.MagicTakePercent,
                    CureIntensity: dto.CureIntensity),
            };
            if (!string.IsNullOrWhiteSpace(dto.Intelligence))
                unit.Intelligence = catalog.GetIntelligence(dto.Intelligence);
            if (!string.IsNullOrWhiteSpace(dto.HateRule))
                unit.HateRule = catalog.GetHateRule(dto.HateRule);

            foreach (var skillKey in dto.Skills) {
                if (!_skillsByKey.TryGetValue(skillKey, out var skill))
                    throw new InvalidOperationException($"单位 '{dto.ConfigKey}' 引用未知技能 '{skillKey}'");
                unit.Skills = [.. unit.Skills, skill];
            }

            _unitsByKey[(UnitConfigKey)dto.ConfigKey] = unit;
        }
    }

    private void BuildDungeons(ModContentJson content, BehaviorCatalog catalog) {
        foreach (var dto in content.Dungeons) {
            var enemies = dto.Enemies.Select(e => {
                if (!_unitsByKey.TryGetValue(e.Unit, out var unit))
                    throw new InvalidOperationException($"副本 '{dto.Key}' 引用未知单位 '{e.Unit}'");
                return new EnemySpawnConfig(
                    Unit: unit,
                    Count: e.Count,
                    SpawnBaseX: e.SpawnBaseX,
                    SpawnXSpacing: e.SpawnXSpacing);
            }).ToArray();

            var playerCamps = dto.PlayerCamps.Select(c => new PlayerCampOption(c.Key, c.Camps)).ToArray();

            _dungeonsByKey[dto.Key] = new DungeonConfig(
                DungeonKey: dto.Key,
                PlayerCampOptions: playerCamps,
                EnemyCamps: dto.EnemyCamps,
                Enemies: enemies,
                RelationsResolver: catalog.GetCampRelation(dto.Relations),
                Layout: dto.Layout is { } layout
                    ? new BattlefieldLayout(
                        layout.HalfWidth,
                        layout.HalfHeight,
                        [.. layout.Obstacles.Select(o => new ObstacleRect(o.MinX, o.MinY, o.MaxX, o.MaxY))])
                    : BattlefieldLayout.Default);
        }
    }

    private BuffDefinition GetBuffByKeyOrThrow(string buffKey) {
        if (_buffsByKey.TryGetValue(buffKey, out var buff))
            return buff;
        throw new InvalidOperationException($"add_buff 技能引用未知 Buff '{buffKey}'；Buff 必须先于技能编译");
    }

    private static string DtoOrDefault(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"无法解析枚举 {typeof(T).Name} 值 '{value}'");

    private static InvalidOperationException MissingField(string id, string field) =>
        new($"技能 '{id}' 缺少字段 {field}");
}
