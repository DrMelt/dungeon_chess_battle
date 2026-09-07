namespace DungeonChessBattle.Game.Services;

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
