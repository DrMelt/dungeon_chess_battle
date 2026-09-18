namespace DungeonChessBattle.Battle.Shared.ValueObjects;

/// <summary>
/// Buff 身份强类型键。领域、结算与配置层使用字符串键，杜绝裸字符串造成的类型混淆；
/// 网络协议与同步实体边界同样以字符串键序列化，BuffTypeId 为跨端一致的唯一标识，
/// 字段在全链路统一为字符串。
/// 仅声明长度上限与语义名，存储与校验复用 <see cref="RestrictedString"/>。
/// </summary>
public readonly record struct BuffTypeId {
    /// <summary>Buff 键最大字符数，与技能键、单位配置键同长。</summary>
    public const ushort MaxLength = 32;

    private readonly RestrictedString _inner;

    /// <summary>构造 Buff 键；长度不得超过 <see cref="MaxLength"/>，超限抛异常响亮暴露。null 视为无键。</summary>
    public BuffTypeId(string? value) => _inner = new(value, MaxLength);

    /// <summary>Buff 字符串键。</summary>
    public string Value => _inner.Value;

    /// <summary>无 Buff 键，default 语义。用于占位定义与未声明判定。</summary>
    public static BuffTypeId None => default;

    /// <summary>是否无有效 Buff 键。</summary>
    public bool IsDefault => _inner.IsDefault;

    /// <summary>Buff 键字符串隐式转强类型，空串/超限由构造校验承载。</summary>
    public static implicit operator BuffTypeId(string? value) => new(value);

    /// <summary>强类型隐式转 Buff 键字符串，供网络与持久化边界取回原值。</summary>
    public static implicit operator string(BuffTypeId id) => id.Value;

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();
}
