using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.GameConfig.Models;
using DungeonChessBattle.Battle.Mod;
using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 内容编译层：把合并后的 <see cref="ModContentJson"/> 一键映射为领域只读定义
/// （BuffDefinition / SkillDefinition / UnitConfig / DungeonConfig）与各向索引。
/// 行为字段一律经 <see cref="BehaviorCatalog"/> 解析，mod 数据与内置数据同走本层。
/// 编译即校验：引用缺失、未知行为 ID、非唯一 BuffTypeId 都在构造期抛异常大声失败。
/// </summary>
public sealed partial class ContentSetRegistry {
    private readonly Dictionary<string, BuffDefinition> _buffsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, BuffDefinition> _buffsByTypeId = [];
    private readonly Dictionary<string, SkillDefinition> _skillsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<UnitConfigKey, UnitConfig> _unitsByKey = [];
    private readonly Dictionary<string, DungeonConfig> _dungeonsByKey = new(StringComparer.Ordinal);

    /// <summary>编译合格的内容集。</summary>
    public ModContentJson Content {
        get;
    }

    /// <summary>内容修订：基座修订号 + 启用 mod 指纹短前缀，配置与布局任何变化都会改变值。</summary>
    public string DataRevision {
        get;
    }

    /// <summary>建造内容注册表；编译失败抛异常。</summary>
    public ContentSetRegistry(ModContentJson content, string builtInRevision, string modFingerprint, BehaviorCatalog catalog) {
        Content = content;
        DataRevision = string.IsNullOrEmpty(modFingerprint)
            ? builtInRevision
            : $"{builtInRevision}+{modFingerprint}";

        BuildBuffs(content, catalog);
        BuildSkills(content, catalog);
        BuildUnits(content, catalog);
        BuildDungeons(content, catalog);
    }

    /// <summary>全部技能定义。</summary>
    public IReadOnlyCollection<SkillDefinition> Skills => _skillsByKey.Values;

    /// <summary>全部 Buff 定义。</summary>
    public IReadOnlyCollection<BuffDefinition> Buffs => _buffsByKey.Values;

    /// <summary>全部单位配置。</summary>
    public IReadOnlyCollection<UnitConfig> Units => _unitsByKey.Values;

    /// <summary>全部副本配置。</summary>
    public IReadOnlyCollection<DungeonConfig> Dungeons => _dungeonsByKey.Values;

    /// <summary>按技能键取定义；不存在返回 null。</summary>
    public SkillDefinition? GetSkill(SkillKeyId key) => _skillsByKey.GetValueOrDefault(key.Id);

    /// <summary>按 BuffTypeId 取定义；不存在返回 null。</summary>
    public BuffDefinition? GetBuff(ushort typeId) => _buffsByTypeId.GetValueOrDefault(typeId);

    /// <summary>按 Buff 键取定义；不存在返回 null。</summary>
    public BuffDefinition? GetBuffByKey(string buffKey) => _buffsByKey.GetValueOrDefault(buffKey);

    /// <summary>按单位配置键取配置；不存在返回 null。</summary>
    public UnitConfig? GetUnit(UnitConfigKey key) => _unitsByKey.GetValueOrDefault(key);

    /// <summary>按副本键取配置；不存在返回 null。</summary>
    public DungeonConfig? GetDungeon(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : _dungeonsByKey.GetValueOrDefault(key);

    private void BuildBuffs(ModContentJson content, BehaviorCatalog catalog) {
        foreach (var dto in content.Buffs) {
            BuffDefinition buff = dto.Kind switch {
                "dot" => new DamageOverTimeBuff {
                    BuffTypeId = dto.BuffTypeId,
                    Duration = dto.Duration,
                    MaxStacks = dto.MaxStacks,
                    DamageType = Parse<DamageType>(dto.DamageType),
                    DamagePerSec = dto.DamagePerSec,
                    Effect = catalog.GetBuffEffect(DtoOrDefault(dto.Effect, BehaviorIds.BuffEffect.Dot)),
                },
                "hot" => new HealOverTimeBuff {
                    BuffTypeId = dto.BuffTypeId,
                    Duration = dto.Duration,
                    MaxStacks = dto.MaxStacks,
                    HealthPerSec = dto.HealthPerSec,
                    Effect = catalog.GetBuffEffect(DtoOrDefault(dto.Effect, BehaviorIds.BuffEffect.Hot)),
                },
                _ => throw new InvalidOperationException($"Buff '{dto.Id}' 未知 Kind '{dto.Kind}'"),
            };

            _buffsByKey[dto.Id] = buff;
            _buffsByTypeId[dto.BuffTypeId] = buff;
        }
    }
}
