using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 服务器逻辑 tick 与剩余秒数的换算工具。
/// 倒计时同步统一为 EndServerTick：服务端写入截止 tick，客户端与服务端本地推算剩余，避免每 tick 全量推送当前值。
/// 截止 tick 的 0 是「无倒计时」哨兵：剩余秒非正一律写 0，反算见 0 直接归零，不参与 tick 差值。
/// 缺这条哨兵时非正剩余会被折算成当前 tick，而客户端 ServerTick 恒落后服务端一个流水深度，
/// 反算出的剩余秒便恒等于该落后量、永不收敛。哨兵与 tick 自然回绕的 0 相撞，代价是每 65536 tick
/// 一个 tick 宽度内被读成无倒计时，为此不值得扩字段宽。
/// </summary>
public static class SyncTickHelper {
    /// <summary>无倒计时哨兵。</summary>
    private const ushort NoDeadline = 0;

    /// <summary>距截止 tick 的剩余秒数；客户端用插值 ServerTick，服务端用 Tick。哨兵与已到期均返回 0。</summary>
    public static float RemainingSeconds(EntityManager em, ushort endServerTick) {
        if (endServerTick == NoDeadline)
            return 0f;
        ushort now = em.IsClient ? ((ClientEntityManager)em).ServerTick : em.Tick;
        int diff = Utils.SequenceDiff(endServerTick, now);
        return diff > 0 ? diff / (float)em.Tickrate : 0f;
    }

    /// <summary>把剩余秒数换算为截止 tick，服务端写入用。向上取整避免提前归零；非正剩余落哨兵。</summary>
    public static ushort EndTick(EntityManager em, float remainingSeconds) {
        if (remainingSeconds <= 0f)
            return NoDeadline;
        int ticks = (int)MathF.Ceiling(remainingSeconds * em.Tickrate);
        return (ushort)(em.Tick + ticks);
    }
}
