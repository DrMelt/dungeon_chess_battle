using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 技能类型强类型 ID。领域、判定与配置层使用字符串键，杜绝裸字符串造成的类型混淆；
/// 网络协议与同步实体边界同样以字符串键序列化，SkillKeyId 为跨端一致的唯一标识，
/// 字段在全链路统一为字符串，不再有 ushort 数值编码。
/// 仅声明长度上限与语义名，存储与校验复用 <see cref="RestrictedString"/>。
/// </summary>
public readonly record struct SkillKeyId {
    /// <summary>技能键最大字符数，与配置表键长对齐。</summary>
    public const ushort MaxKeyLength = 32;

    private readonly RestrictedString _inner;

    /// <summary>构造技能键；长度不得超过 <see cref="MaxKeyLength"/>，超限抛异常响亮暴露。null 视为无键。</summary>
    public SkillKeyId(string? id) => _inner = new(id, MaxKeyLength);

    /// <summary>技能字符串键。</summary>
    public string Id => _inner.Value;

    /// <summary>无技能键，default 语义。用于判定无施法或无技能场景。</summary>
    public static SkillKeyId None => default;

    /// <summary>是否无有效技能键。</summary>
    public bool IsDefault => _inner.IsDefault;

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();
}

