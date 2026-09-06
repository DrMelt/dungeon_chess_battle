namespace DungeonChessBattle.Battle.Shared.ValueObjects;

/// <summary>
/// 受限长度字符串的共用载荷：承载字符串值并在构造时校验长度，
/// 集中 IsDefault/ToString/值相等逻辑，供各受限字符串业务类型复用。
/// </summary>
public readonly record struct RestrictedString {
    private readonly string? _value;

    /// <summary>构造受限字符串；长度不得超过 <paramref name="maxLength"/>，超限抛异常响亮暴露。</summary>
    /// <param name="value">字符串值，null 视为空。</param>
    /// <param name="maxLength">最大字符数上限。</param>
    public RestrictedString(string? value, ushort maxLength) {
        if ((value?.Length ?? 0) > maxLength)
            throw new ArgumentException($"字符串长度 {value?.Length ?? 0} 超过上限 {maxLength}。", nameof(value));
        _value = value;
    }

    /// <summary>字符串值，null 归一为空串。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>是否无有效值。</summary>
    public bool IsDefault => string.IsNullOrEmpty(_value);

    /// <inheritdoc />
    public override string ToString() =>
        IsDefault ? "<none>" : _value ?? string.Empty;

    /// <summary>基于语义值比较，忽略内部存储差异。</summary>
    public bool Equals(RestrictedString other) => Value == other.Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}
