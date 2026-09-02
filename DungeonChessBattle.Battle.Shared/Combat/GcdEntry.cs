namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>全局冷却组条目：组键与剩余秒数，服务端权威状态，原地推进避免每帧分配。</summary>
public sealed class GcdEntry(string groupKey, float remaining) {
    /// <summary>全局冷却组键，同组技能共享通道。</summary>
    public string GroupKey = groupKey;

    /// <summary>剩余冷却秒数。</summary>
    public float Remaining = remaining;
}
