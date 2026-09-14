namespace DungeonChessBattle.Battle.Shared.ValueObjects;

/// <summary>
/// 阵营标识强类型键，与副本键、单位配置键同款：仅声明长度上限与语义名，存储与校验复用 <see cref="RestrictedString"/>。
/// 一个单位可属于多个阵营，阵营列表即本类型的列表；取值由内容包定义，引擎不持有取值白名单。
/// 网络协议、回放归档与日志边界仍以字符串携带阵营标识。
/// </summary>
public readonly record struct CampId {
    /// <summary>阵营标识最大字符数，与其余内容键同长。</summary>
    public const ushort MaxLength = 32;

    private readonly RestrictedString _inner;

    /// <summary>构造阵营标识；长度不得超过 <see cref="MaxLength"/>，超限抛异常响亮暴露。null 视为空。</summary>
    public CampId(string? value) => _inner = new(value, MaxLength);

    /// <summary>阵营标识字符串。</summary>
    public string Value => _inner.Value;

    /// <summary>无阵营标识，default 语义。</summary>
    public static CampId None => default;

    /// <summary>是否无有效阵营标识。</summary>
    public bool IsDefault => _inner.IsDefault;

    /// <summary>阵营标识字符串隐式转强类型，空串/超限由构造校验承载。</summary>
    public static implicit operator CampId(string? value) => new(value);

    /// <summary>强类型隐式转阵营标识字符串，供网络、归档与日志边界取回原值。</summary>
    public static implicit operator string(CampId id) => id.Value;

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();
}
