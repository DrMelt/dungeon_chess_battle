using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放浏览服务对"能否取得可重放记录"的裁决结果。失败时 Recording 为 null，Reason 面向日志与提示。
/// 只能在 Game 层组装：下载状态与解码门控在浏览服务内归并为这份结论。
/// 本类型不属 <c>Game.Shared</c>——它持有 <c>ReplayRecording</c>，下沉会把回放模型拖进 mod 的引用闭包。
/// </summary>
public sealed record ReplayPlayableResult(ReplayGateStatus Status, ReplayRecording? Recording = null, string? Reason = null) {
    /// <summary>是否已取得可重放记录。</summary>
    public bool IsReady => Status == ReplayGateStatus.Ready;
}
