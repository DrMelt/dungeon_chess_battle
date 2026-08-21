using System.Collections.Generic;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// RoomBattleClient 的战斗事件日志仓库：保存当前房间会话收到的全部战斗事件，
/// 供 UI 文字化显示读取。追加时机在解码后、事件派发前；会话重置时清空。
/// </summary>
public partial class RoomBattleClient {
    private readonly BattleEventLogStore _eventLog = new();

    /// <summary>当前房间会话的事件日志，UI 据索引做增量同步与历史回填，仅可枚举。</summary>
    public IReadOnlyList<BattleEventLogEntry> GetEventLog() => _eventLog.Entries;

    /// <summary>当前房间会话事件日志的版本号，会话重置时自增。</summary>
    public long GetEventLogVersion() => _eventLog.Version;
}
