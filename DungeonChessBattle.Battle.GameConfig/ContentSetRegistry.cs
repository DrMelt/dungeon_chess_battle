using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 内容注册表：领域定义对象（SkillDefinition / BuffDefinition / UnitConfig / DungeonConfig）
/// 的唯一注册与索引面。内置基座先注册，mod 后注册同键覆盖。
/// 引用以对象图成立：单位持技能定义引用、技能持 Buff 定义引用，注册期不解析字符串。
/// </summary>
/// <remarks>建造空注册表；内置内容与 mod 内容随后按注册顺序填充。</remarks>
public sealed partial class ContentSetRegistry(string builtInRevision, string modFingerprint) {
    private readonly Dictionary<string, SkillDefinition> _skillsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, BuffDefinition> _buffsByTypeId = [];
    private readonly Dictionary<UnitConfigKey, UnitConfig> _unitsByKey = [];
    private readonly Dictionary<string, DungeonConfig> _dungeonsByKey = new(StringComparer.Ordinal);

    /// <summary>内容修订：基座修订号 + 启用 mod 指纹，内容与布局任何变化都会改变值。</summary>
    public string DataRevision {
        get;
    } = string.IsNullOrEmpty(modFingerprint)
            ? builtInRevision
            : $"{builtInRevision}+{modFingerprint}";

    /// <summary>当前默认副本键；未覆盖时沿用基座。注册顺序决定覆盖者。</summary>
    public string DefaultDungeonKey {
        get; private set;
    } = BuiltInContent.DefaultDungeonKey;

    /// <summary>全部技能定义。</summary>
    public IReadOnlyCollection<SkillDefinition> Skills => _skillsByKey.Values;

    /// <summary>全部 Buff 定义。</summary>
    public IReadOnlyCollection<BuffDefinition> Buffs => _buffsByTypeId.Values;

    /// <summary>全部单位配置。</summary>
    public IReadOnlyCollection<UnitConfig> Units => _unitsByKey.Values;

    /// <summary>全部副本配置。</summary>
    public IReadOnlyCollection<DungeonConfig> Dungeons => _dungeonsByKey.Values;

    /// <summary>按技能键取定义；不存在返回 null。</summary>
    public SkillDefinition? GetSkill(SkillKeyId key) => _skillsByKey.GetValueOrDefault(key.Id);

    /// <summary>按 BuffTypeId 取定义；不存在返回 null。</summary>
    public BuffDefinition? GetBuff(ushort typeId) => _buffsByTypeId.GetValueOrDefault(typeId);

    /// <summary>按单位配置键取配置；不存在返回 null。</summary>
    public UnitConfig? GetUnit(UnitConfigKey key) => _unitsByKey.GetValueOrDefault(key);

    /// <summary>按副本键取配置；不存在返回 null。</summary>
    public DungeonConfig? GetDungeon(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : _dungeonsByKey.GetValueOrDefault(key);

    internal void RegisterSkill(SkillDefinition skill) => _skillsByKey[skill.SkillId.Id] = skill;

    /// <summary>注册 Buff 定义（内置基座路径）；BuffTypeId 同键覆盖，零 ID 拒绝。</summary>
    internal void RegisterBuff(BuffDefinition buff) {
        if (buff.BuffTypeId == 0)
            throw new InvalidOperationException("Buff 必须声明非零 BuffTypeId");
        _buffsByTypeId[buff.BuffTypeId] = buff;
    }

    /// <summary>
    /// 注册 mod 声明的 Buff 定义：引擎内置段（1~<see cref="ModBuffTypeIdMin"/>-1）由基座占有，
    /// mod 必须声明 <see cref="ModBuffTypeIdMin"/> 及以上，越段拒载。
    /// </summary>
    internal void RegisterModBuff(BuffDefinition buff) {
        if (buff.BuffTypeId < ModBuffTypeIdMin)
            throw new InvalidOperationException(
                $"mod Buff '{buff.GetType().Name}' 的 BuffTypeId {buff.BuffTypeId} 落在引擎内置段（1~{ModBuffTypeIdMin - 1}），mod 必须声明 {ModBuffTypeIdMin} 及以上");
        RegisterBuff(buff);
    }

    /// <summary>mod 允许的最小 BuffTypeId；引擎内置段为 1~(<see cref="ModBuffTypeIdMin"/> - 1)。</summary>
    public const ushort ModBuffTypeIdMin = 1000;

    internal void RegisterUnit(UnitConfig unit) => _unitsByKey[unit.ConfigKey] = unit;

    internal void RegisterDungeon(DungeonConfig dungeon) => _dungeonsByKey[dungeon.DungeonKey] = dungeon;

    internal void SetDefaultDungeonKey(string key) => DefaultDungeonKey = key;
}
