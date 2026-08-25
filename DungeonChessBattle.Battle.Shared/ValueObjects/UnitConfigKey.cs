namespace DungeonChessBattle.Battle.Shared.ValueObjects;

/// <summary>
/// 单位配置键强类型，键长与配置表 UnitConfig.ConfigKey 对齐。
/// 仅声明长度上限与语义名，存储与校验复用 <see cref="RestrictedString"/>。
/// </summary>
public readonly record struct UnitConfigKey {
    /// <summary>单位配置键最大字符数，与配置表键长对齐。</summary>
    public const ushort MaxLength = 32;

    private readonly RestrictedString _inner;

    /// <summary>构造配置键；长度不得超过 <see cref="MaxLength"/>，超限抛异常响亮暴露。</summary>
    public UnitConfigKey(string? value) => _inner = new(value, MaxLength);

    /// <summary>单位配置键字符串。</summary>
    public string Value => _inner.Value;

    /// <summary>无配置键，default 语义。</summary>
    public static UnitConfigKey None => default;

    /// <summary>是否无有效配置键。</summary>
    public bool IsDefault => _inner.IsDefault;

    /// <summary>配置键字符串隐式转强类型，空串/超限由构造校验承载。</summary>
    public static implicit operator UnitConfigKey(string? value) => new(value);

    /// <summary>强类型隐式转配置键字符串，供网络与持久化边界取回原值。</summary>
    public static implicit operator string(UnitConfigKey key) => key.Value;

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();
}
