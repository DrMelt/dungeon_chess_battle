namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放"能否播放"的门控结论。与传输态 <c>Replay.Client.ReplayTransportStatus</c> 解耦：
/// 传输只答"拿没拿到字节"，本态裁决"这份字节能不能重放"。
/// </summary>
public enum ReplayGateStatus {
    /// <summary>取得可重放的记录。</summary>
    Ready,

    /// <summary>本地无副本且下载未到，尚无内容可裁决。</summary>
    NotCached,

    /// <summary>字节流不合容器规范：本地缓存损坏、被截断或不是回放文件。</summary>
    Corrupted,

    /// <summary>回放格式版本不由本机读取，须由录制端重录，重下也无用。</summary>
    Unsupported,

    /// <summary>内容或结算逻辑修订号与本地不一致，重放会漂移。</summary>
    Incompatible,
}
