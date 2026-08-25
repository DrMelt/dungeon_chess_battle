using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 服务器逻辑 tick 与剩余秒数的换算工具。
/// 倒计时同步统一为 EndServerTick：服务端写入截止 tick，客户端与服务端本地推算剩余，避免每 tick 全量推送当前值。
/// </summary>
public static class SyncTickHelper {
    /// <summary>距截止 tick 的剩余秒数；客户端用插值 ServerTick，服务端用 Tick。已到期返回 0。</summary>
    public static float RemainingSeconds(EntityManager em, ushort endServerTick) {
        ushort now = em.IsClient ? ((ClientEntityManager)em).ServerTick : em.Tick;
        int diff = Utils.SequenceDiff(endServerTick, now);
        return diff > 0 ? diff / (float)em.Tickrate : 0f;
    }

    /// <summary>把剩余秒数换算为截止 tick，服务端写入用。向上取整避免提前归零。</summary>
    public static ushort EndTick(EntityManager em, float remainingSeconds) {
        int ticks = Math.Max(0, (int)MathF.Ceiling(remainingSeconds * em.Tickrate));
        return (ushort)(em.Tick + ticks);
    }
}
