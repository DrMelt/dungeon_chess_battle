namespace DungeonChessBattle.Session.Shared;

/// <summary>
/// 房间标识强类型。会话期由大厅端生成，长度与 Guid "N" 格式对齐；
/// 协议 DTO、实体同步线缆、回放归档与 HTTP 路由仍以字符串携带房间标识，本类型是会话侧唯一身份。
/// </summary>
public readonly record struct RoomId {
    /// <summary>房间标识最大字符数，与 Guid "N" 格式对齐。</summary>
    public const ushort MaxLength = 32;

    private readonly string? _value;

    /// <summary>构造房间标识；长度不得超过 <see cref="MaxLength"/>，超限抛异常响亮暴露。null 视为无标识。</summary>
    public RoomId(string? value) {
        if ((value?.Length ?? 0) > MaxLength)
            throw new ArgumentException($"房间标识长度 {value?.Length ?? 0} 超过上限 {MaxLength}。", nameof(value));
        _value = value;
    }

    /// <summary>尝试构造房间标识；空或超长返回 null，不抛异常。供客户端提交、线缆同步与路由参数等不可信输入在转换前判定。</summary>
    /// <param name="value">房间标识字符串，null 视为空。</param>
    public static RoomId? TryCreate(string? value) {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
            return null;
        return new RoomId(value);
    }

    /// <summary>房间标识字符串，null 归一为空串。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>无房间标识，default 语义。</summary>
    public static RoomId None => default;

    /// <summary>是否无有效房间标识。</summary>
    public bool IsDefault => string.IsNullOrEmpty(_value);

    /// <summary>房间标识字符串隐式转强类型，空串与超限由构造校验承载。</summary>
    public static implicit operator RoomId(string? value) => new(value);

    /// <summary>强类型隐式转房间标识字符串，供协议、同步线缆、归档与路由边界取回原值。</summary>
    public static implicit operator string(RoomId id) => id.Value;

    /// <summary>基于语义值比较，忽略 null 与空串的存储差异。</summary>
    public bool Equals(RoomId other) => Value == other.Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Value;
}
