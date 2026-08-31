namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 单位网络实体 ID 强类型：房间内唯一，领域事件与仇恨账本统一标识。
/// 杜绝裸 ushort 造成的类型混淆（与 BuffTypeId、逻辑 tick 等区分）。
/// 网络与回放边界以原生 ushort 承载，进出领域经双向隐式转换收放。
/// </summary>
public readonly record struct UnitId {
    private readonly ushort _value;

    /// <summary>构造单位网络实体 ID。</summary>
    public UnitId(ushort value) => _value = value;

    /// <summary>原生 ushort 值，供网络与持久化边界取回原值。</summary>
    public ushort Value => _value;

    /// <summary>无有效单位 ID，原生值 0；边界写出时仍落 0。</summary>
    public static UnitId None => default;

    /// <summary>是否无有效单位 ID。</summary>
    public bool IsDefault => _value == 0;

    /// <summary>单位 ID 隐式转强类型，网络边界读入用。</summary>
    public static implicit operator UnitId(ushort value) => new(value);

    /// <summary>强类型隐式转单位 ID，网络边界写出与字典查询用。</summary>
    public static implicit operator ushort(UnitId id) => id._value;

    /// <inheritdoc />
    public override string ToString() => _value.ToString();
}
