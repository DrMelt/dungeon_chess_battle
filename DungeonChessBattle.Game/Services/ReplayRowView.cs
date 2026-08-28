using System.Collections.Generic;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放行视图：由回放浏览服务单点裁决产出，承载一行卡片的呈现与可用态结论。
/// 表现层只读本视图渲染，不再自行组合在途/缓存/内容版本等业务规则。
/// 静态部分（房间/副本/时间/玩家/是否仅本地）用于摘要，动态部分（动作语义/可用态）用于按钮。
/// 文案由视图层按 <see cref="Action"/> 与 <see cref="DownloadPercent"/> 翻译。
/// </summary>
/// <param name="RoomId">房间 ID，回放主键。</param>
/// <param name="DungeonKey">副本键。</param>
/// <param name="StartUnixTime">战斗开始时间，Unix 秒，UTC。</param>
/// <param name="TickRate">逻辑 tick 频率。</param>
/// <param name="PlayerNames">参与玩家名。</param>
/// <param name="FromServer">服务端归档是否仍可重下；false 表示只剩本地副本。</param>
/// <param name="Action">该行动作语义，视图层据此翻译文案与可用态。</param>
/// <param name="PlayEnabled">播放按钮是否可点。</param>
/// <param name="DownloadPercent">下载进度百分比 0~100；未知为 null。</param>
public sealed record ReplayRowView(
    string RoomId,
    string DungeonKey,
    long StartUnixTime,
    int TickRate,
    IReadOnlyList<string> PlayerNames,
    bool FromServer,
    ReplayBrowseAction Action,
    bool PlayEnabled,
    int? DownloadPercent);
