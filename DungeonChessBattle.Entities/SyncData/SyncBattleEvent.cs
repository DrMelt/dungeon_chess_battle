namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 战斗事件日志的扁平化同步结构，unmanaged 定长槽位。
/// 整帧事件日志编码为 SyncBattleEvent 数组经传输层可靠通道外送，帧内顺序即服务端产出顺序。
/// Type 为事件类型 tag，A/B/C 为 ushort 槽位，Value 为 float 槽位；槽位语义由 BattleEventCoder 集中映射。
/// </summary>
public struct SyncBattleEvent {
    /// <summary>事件类型 tag，对应 BattleEventCoder.Type* 常量。</summary>
    public byte Type;

    /// <summary>语义随 Type：来源/目标/持有者/施法者/单位网络 ID。</summary>
    public ushort A;

    /// <summary>语义随 Type：目标/Buff 类型/技能 ID/操作码。</summary>
    public ushort B;

    /// <summary>语义随 Type：伤害类型/仇恨操作/叠加层数/施法目标，0 表示无。</summary>
    public ushort C;

    /// <summary>数值槽位：伤害量/治疗量/仇恨值。</summary>
    public float Value;
}
