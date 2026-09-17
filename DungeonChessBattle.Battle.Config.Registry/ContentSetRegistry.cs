using DungeonChessBattle.Battle.Config.Shared;
using DungeonChessBattle.Battle.Config.Shared.Buffs;
using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Config.Shared.Content;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Config.Registry;

/// <summary>
/// 内容注册表：领域定义对象（SkillDefinition / BuffDefinition / UnitConfig / DungeonConfig）
/// 的唯一注册与索引面，内容全部由外部写入，同键后写覆盖。
/// 引用以对象图成立：单位持技能定义引用、技能持 Buff 定义引用，注册期不解析字符串。
/// 玩家可选性由内容经注册动作写入，不由单位配置表达，也不由引擎按其他字段推断。
/// </summary>
/// <remarks>注册表自空开始，内容按写入顺序覆盖。</remarks>
public sealed partial class ContentSetRegistry(string engineRevision, string contentFingerprint) : IContentRegistryView {
    private readonly Dictionary<SkillKeyId, SkillDefinition> _skillsByKey = [];
    private readonly Dictionary<BuffTypeId, BuffDefinition> _buffsByKey = [];
    private readonly Dictionary<UnitConfigKey, UnitConfig> _unitsByKey = [];
    private readonly Dictionary<DungeonKeyId, DungeonConfig> _dungeonsByKey = [];

    /// <summary>玩家可选单位名册，注册次序即展示次序，同键只登记一次。</summary>
    private readonly List<UnitConfigKey> _playerSelectable = [];

    /// <summary>内容修订：引擎内容修订号 + 装配方传入的内容指纹，内容与布局任何变化都会改变值。</summary>
    public string DataRevision {
        get;
    } = string.IsNullOrEmpty(contentFingerprint)
            ? engineRevision
            : $"{engineRevision}+{contentFingerprint}";

    /// <summary>全部技能定义。</summary>
    public IReadOnlyCollection<SkillDefinition> Skills => _skillsByKey.Values;

    /// <summary>全部 Buff 定义。</summary>
    public IReadOnlyCollection<BuffDefinition> Buffs => _buffsByKey.Values;

    /// <summary>全部单位配置。</summary>
    public IReadOnlyCollection<UnitConfig> Units => _unitsByKey.Values;

    /// <summary>全部副本配置。</summary>
    public IReadOnlyCollection<DungeonConfig> Dungeons => _dungeonsByKey.Values;

    /// <summary>按技能键取定义；不存在返回 null。</summary>
    public SkillDefinition? GetSkill(SkillKeyId skillKey) => _skillsByKey.GetValueOrDefault(skillKey);

    /// <summary>按技能键取定义；不存在抛异常，装配期与内容消费方必得。</summary>
    public SkillDefinition GetRequiredSkill(SkillKeyId skillKey) =>
        GetSkill(skillKey) ?? throw new InvalidOperationException($"技能 '{skillKey.Id}' 未注册。");

    /// <summary>按 Buff 键取定义；不存在返回 null。</summary>
    public BuffDefinition? GetBuff(BuffTypeId buffTypeId) => _buffsByKey.GetValueOrDefault(buffTypeId);

    /// <summary>按 Buff 键取定义；不存在抛异常，装配期与内容消费方必得。</summary>
    public BuffDefinition GetRequiredBuff(BuffTypeId buffTypeId) =>
        GetBuff(buffTypeId) ?? throw new InvalidOperationException($"Buff '{buffTypeId.Value}' 未注册。");

    /// <summary>按单位配置键取配置；不存在返回 null。</summary>
    public UnitConfig? GetUnit(UnitConfigKey configKey) => _unitsByKey.GetValueOrDefault(configKey);

    /// <summary>按单位配置键取配置；不存在抛异常，装配期与内容消费方必得。</summary>
    public UnitConfig GetRequiredUnit(UnitConfigKey configKey) =>
        GetUnit(configKey) ?? throw new InvalidOperationException($"单位 '{configKey.Value}' 未注册。");

    /// <summary>单位是否可被玩家在准备阶段选择。</summary>
    public bool IsPlayerSelectable(UnitConfigKey configKey) => _playerSelectable.Contains(configKey);

    /// <summary>取玩家可选单位，按名册登记次序回查注册表现值。</summary>
    public IReadOnlyList<UnitConfig> GetPlayerSelectableUnits() =>
        [.. _playerSelectable.Select(GetRequiredUnit)];

    /// <summary>按副本键取配置；无键或不存在返回 null。</summary>
    public DungeonConfig? GetDungeon(DungeonKeyId dungeonKey) => _dungeonsByKey.GetValueOrDefault(dungeonKey);

    /// <summary>按副本键取配置；不存在抛异常，装配期与内容消费方必得。</summary>
    public DungeonConfig GetRequiredDungeon(DungeonKeyId dungeonKey) =>
        GetDungeon(dungeonKey) ?? throw new InvalidOperationException($"副本 '{dungeonKey.Value}' 未注册。");

    /// <summary>注册技能定义：同 SkillId 覆盖，空键拒绝。</summary>
    public void RegisterSkill(SkillDefinition skill) {
        if (skill.SkillId.IsDefault)
            throw new InvalidOperationException("技能必须声明技能键");
        _skillsByKey[skill.SkillId] = skill;
    }

    /// <summary>注册 Buff 定义：同键覆盖，空键拒绝。</summary>
    public void RegisterBuff(BuffDefinition buff) {
        if (buff.BuffTypeId.IsDefault)
            throw new InvalidOperationException("Buff 必须声明 Buff 键");
        _buffsByKey[buff.BuffTypeId] = buff;
    }

    /// <summary>注册单位配置：同 ConfigKey 覆盖，空键拒绝。</summary>
    public void RegisterUnit(UnitConfig unit) {
        if (unit.ConfigKey.IsDefault)
            throw new InvalidOperationException("单位必须声明配置键");
        _unitsByKey[unit.ConfigKey] = unit;
    }

    /// <summary>注册玩家可选单位：注册单位并按注册次序登记名册，同键覆盖单位、名册只登记一次。</summary>
    public void RegisterPlayerSelectableUnit(UnitConfig unit) {
        RegisterUnit(unit);
        if (!_playerSelectable.Contains(unit.ConfigKey))
            _playerSelectable.Add(unit.ConfigKey);
    }

    /// <summary>注册副本配置：同 DungeonKey 覆盖，空键拒绝。</summary>
    public void RegisterDungeon(DungeonConfig dungeon) {
        if (dungeon.DungeonKey.IsDefault)
            throw new InvalidOperationException("副本必须声明副本键");
        _dungeonsByKey[dungeon.DungeonKey] = dungeon;
    }
}
