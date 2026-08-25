namespace DungeonChessBattle.Battle.Shared.ValueObjects;

/// <summary>
/// 玩家昵称强类型。仅声明长度上限与语义名，存储与校验复用 <see cref="RestrictedString"/>，
/// 服务端校验与客户端 UI 限制共用同一约束，杜绝两端漂移。
/// </summary>
public readonly record struct PlayerName {
    /// <summary>玩家昵称最大字符数。</summary>
    public const ushort MaxLength = 16;

    private readonly RestrictedString _inner;

    /// <summary>构造玩家昵称；长度不得超过 <see cref="MaxLength"/>，超限抛异常响亮暴露。</summary>
    public PlayerName(string? value) => _inner = new(value, MaxLength);

    /// <summary>玩家昵称。</summary>
    public string Value => _inner.Value;

    /// <summary>无昵称，default 语义。</summary>
    public static PlayerName None => default;

    /// <summary>是否无有效昵称。</summary>
    public bool IsDefault => _inner.IsDefault;

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();
}
