namespace DungeonChessBattle.Battle.Shared.ValueObjects;

/// <summary>
/// 副本键强类型，与 Buff 键、单位配置键同款：仅声明长度上限与语义名，存储与校验复用 <see cref="RestrictedString"/>。
/// 网络协议与持久化边界仍以字符串携带副本键，DungeonKeyId 是内容侧唯一身份。
/// </summary>
public readonly record struct DungeonKeyId {
    /// <summary>副本键最大字符数，与其余内容键同长。</summary>
    public const ushort MaxLength = 32;

    private readonly RestrictedString _inner;

    /// <summary>构造副本键；长度不得超过 <see cref="MaxLength"/>，超限抛异常响亮暴露。null 视为无键。</summary>
    public DungeonKeyId(string? value) => _inner = new(value, MaxLength);

    /// <summary>副本字符串键。</summary>
    public string Value => _inner.Value;

    /// <summary>无副本键，default 语义。用于未选定副本与占位定义。</summary>
    public static DungeonKeyId None => default;

    /// <summary>是否无有效副本键。</summary>
    public bool IsDefault => _inner.IsDefault;

    /// <summary>副本键字符串隐式转强类型，空串/超限由构造校验承载。</summary>
    public static implicit operator DungeonKeyId(string? value) => new(value);

    /// <summary>强类型隐式转副本键字符串，供网络与持久化边界取回原值。</summary>
    public static implicit operator string(DungeonKeyId id) => id.Value;

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();
}
