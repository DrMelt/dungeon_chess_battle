namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 技能个体冷却的扁平化同步数据。
/// SyncList 以非托管内存布局直接传输，本结构仅含 unmanaged 标量字段。
/// 服务端权威写入截止 tick，客户端按当前服务器 tick 本地推算剩余秒数。
/// </summary>
public struct SyncSkillCooldown {
    /// <summary>技能配置 ID。</summary>
    public ushort SkillId;

    /// <summary>冷却截止的服务器逻辑 tick。</summary>
    public ushort EndServerTick;
}

