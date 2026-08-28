using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放"能否播放"的门控结论，归回放浏览服务。与传输态 <see cref="DungeonChessBattle.Replay.Client.ReplayTransportStatus"/> 解耦：
/// 传输只答"拿没拿到字节"，本态裁决"这份字节能不能重放"。
/// </summary>
public enum ReplayGateStatus {
    /// <summary>取得可重放的快照。</summary>
    Ready,

    /// <summary>本地无副本且下载未到，尚无内容可裁决。</summary>
    NotCached,

    /// <summary>字节流解码失败：本地缓存损坏或记录格式不兼容。</summary>
    Corrupted,

    /// <summary>内容数据修订号与本地配置不一致，重放会漂移。</summary>
    Incompatible,
}

/// <summary>
/// 回放浏览服务对"能否取得可重放快照"的裁决结果。失败时 Snapshot 为 null，Reason 面向日志与提示。
/// 只能在 Game 层组装：下载状态与解码门控在浏览服务内归并为这份结论。
/// </summary>
public sealed record ReplayPlayableResult(ReplayGateStatus Status, ReplayRecordSnapshot? Snapshot = null, string? Reason = null) {
    /// <summary>是否已取得可重放快照。</summary>
    public bool IsReady => Status == ReplayGateStatus.Ready;
}

/// <summary>
/// 一行回放卡片的动作语义，由回放浏览服务单点裁决。视图层据此翻译文案与按钮可用态，不再自组合规则。
/// </summary>
public enum ReplayBrowseAction {
    /// <summary>无可用动作。</summary>
    None,

    /// <summary>可发起下载，下载按钮可点。</summary>
    Download,

    /// <summary>下载在途，期间不可再点。</summary>
    Downloading,

    /// <summary>可启动回放，播放按钮可点。</summary>
    Play,

    /// <summary>内容版本不符，不可下载亦不可播放。</summary>
    Blocked,
}
