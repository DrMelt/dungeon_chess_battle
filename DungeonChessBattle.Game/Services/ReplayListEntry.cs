using System.Collections.Generic;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放列表静态摘要快照：服务端归档与本地缓存合并后的内部形状，含两项修订号用于可用性裁决。
/// 面板不直接消费——<see cref="ReplayService"/> 据此快照产出 <see cref="ReplayRowView"/> 行视图。
/// 服务端条目与本地条目均由归档元数据块投影而来，同一字段不随来源改变语义。
/// </summary>
/// <param name="RoomId">房间 ID，回放主键，也是本地副本文件名。</param>
/// <param name="DungeonKey">副本键。</param>
/// <param name="StartUnixTime">战斗开始时间，Unix 秒，UTC。</param>
/// <param name="TickRate">逻辑 tick 频率。</param>
/// <param name="DurationTicks">回放覆盖的逻辑帧数，时长即其除以 TickRate。</param>
/// <param name="DataVersion">录制端内容数据修订号，与本地不一致时不可重放。</param>
/// <param name="LogicVersion">录制端结算逻辑修订号，与本地不一致时不可重放。</param>
/// <param name="PlayerNames">参与玩家名。归属主键不由本条目携带：归档元数据里没有它。</param>
/// <param name="FromServer">服务端归档是否还有这条；false 表示只剩本地副本，删了就没有第二次。</param>
internal sealed record ReplayListEntry(
    string RoomId,
    string DungeonKey,
    long StartUnixTime,
    int TickRate,
    int DurationTicks,
    string DataVersion,
    string LogicVersion,
    IReadOnlyList<string> PlayerNames,
    bool FromServer);
