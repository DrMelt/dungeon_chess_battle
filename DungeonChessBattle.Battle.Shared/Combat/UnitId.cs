namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 单位网络实体 ID 强类型：房间内唯一，领域事件、仇恨账本与玩家命令统一标识。
/// 0 恒非法——LiteEntitySystem 的同步实体 ID 从 1 起分配，故 <see cref="None"/>/<see cref="IsDefault"/>
/// 即「无单位」，边界侧的裸 0 与它同义。ID 会被回收复用，只在本房间本次运行内有意义。
/// 本类型不得进 SyncVar 与 MessagePack：LES 只注册了 ushort 值处理器，包装类型无法序列化，
/// 故线协议、同步实体与回放条目一律以原生 ushort 承载，进出领域经双向隐式转换收放。
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
