using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GamePlayUI.battle_event_log;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 战斗事件日志面板：文字化显示当前房间会话的全部战斗事件。
/// 从 BattleSessionContext 事件日志投影按会话版本与显示游标增量同步，版本变化（会话重置）游标归零重同步，
/// 打开面板自动回填历史；
/// 绑定切换由会话 IsInBattle 变化驱动，进出战斗自动显示/隐藏，退出战斗隐藏。
/// UI 节点树定义于 battle_event_log_panel.tscn，本脚本只承载业务逻辑。F4 切换显隐。
/// </summary>
public partial class BattleEventLogPanel : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleEventLogPanel> _logger = ServiceLocator.GetLogger<BattleEventLogPanel>();

    /// <summary>显隐切换键。</summary>
    private const Key ToggleKey = Key.F4;

    /// <summary>日志文本最大保留条数，超出丢弃旧行，防止 RichTextLabel 无限增长。</summary>
    private const int MaxTextLines = 500;

    /// <summary>Buff 资源表，按 BuffTypeId 解析名称，懒加载。</summary>
    private static readonly Lazy<BuffResourceTable?> _buffTable = new(
        () => GD.Load<BuffResourceTable>("res://GameAssets/Buffs/res_buff_resource_table.tres"));

    /// <summary>战斗会话上下文引用，提供事件日志投影与单位名称映射。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>导出引用集合节点。</summary>
    public BattleEventLogPanelInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>上一帧是否在战斗中，用于检测进出战斗切换。</summary>
    private bool _wasInBattle;

    /// <summary>已同步到面板的事件条数游标。</summary>
    private int _shownCount;

    /// <summary>已消费日志仓库的会话版本号，版本变化即会话重置，游标归零重同步。</summary>
    private long _seenVersion;

    /// <summary>已格式化的文本行，超上限时丢弃旧行。</summary>
    private readonly List<string> _lines = [];

    /// <summary>是否有新增文本待重建显示。</summary>
    private bool _dirty;

    /// <summary>节点就绪：获取引用集合节点并校验导出引用。</summary>
    public override void _Ready() {
        InterRefs = GetNode<BattleEventLogPanelInterRefs>("BattleEventLogPanelInterRefs");
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
    }

    /// <summary>每帧同步事件日志：进出战斗切换绑定，追加新增条目并重建文本。</summary>
    public override void _Process(double delta) {
        var session = _sessionRef;
        if (session == null)
            return;

        bool inBattle = session.IsInBattle;
        if (inBattle != _wasInBattle) {
            _wasInBattle = inBattle;
            ResetDisplay(session.EventLogVersion);
            Visible = inBattle;
        }

        if (inBattle)
            SyncNewEvents(session);
    }

    /// <summary>处理显隐切换快捷键。</summary>
    public override void _UnhandledInput(InputEvent @event) {
        if (@event is InputEventKey { Pressed: true, PhysicalKeycode: ToggleKey, Echo: false })
            Visible = !Visible;
    }

    /// <summary>复位面板显示与游标，服务切换或仓库会话版本变化时调用；重建延迟到 SyncNewEvents 末尾统一执行。</summary>
    private void ResetDisplay(long version) {
        _seenVersion = version;
        _shownCount = 0;
        _lines.Clear();
        _dirty = true;
    }

    /// <summary>从会话投影同步尚未显示的事件；会话版本变化（清空）时游标归零重同步。</summary>
    private void SyncNewEvents(BattleSessionContext session) {
        long version = session.EventLogVersion;
        if (version != _seenVersion)
            ResetDisplay(version);
        var entries = session.EventLog;
        for (; _shownCount < entries.Count; _shownCount++)
            AppendLine(entries[_shownCount]);
        RebuildLabel();
    }

    private void AppendLine(BattleEventLogEntry entry) {
        string time = FormatTime(entry);
        string text = BattleEventLogTextFormatter.Format(entry, ResolveUnitName, ResolveSkillName, ResolveBuffName);
        _lines.Add($"{time}{text}");
        if (_lines.Count > MaxTextLines)
            _lines.RemoveAt(0);
        _dirty = true;
    }

    /// <summary>有新增文本时重建日志显示，保持滚动跟随最新。</summary>
    private void RebuildLabel() {
        if (InterRefs?.LogLabelRef is not { } label || !_dirty)
            return;
        _dirty = false;
        label.Clear();
        foreach (var line in _lines)
            label.AddText(line + "\n");
        label.ScrollFollowing = true;
    }

    /// <summary>把条目接收时刻格式化为相对战斗开始秒数的文本前缀。</summary>
    private string FormatTime(BattleEventLogEntry entry) {
        long? battleStartMs = _sessionRef?.BattleStartUnixTime is { } start ? start * 1000L : null;
        double elapsed = battleStartMs is { } ms && ms > 0
            ? Math.Max(0, (entry.ReceiveUnixMs - ms) / 1000.0)
            : 0;
        return $"[{elapsed:0.0}] ";
    }

    /// <summary>按单位网络 ID 解析单位名；会话中未找到回退为裸 ID。</summary>
    private string ResolveUnitName(ushort netId) {
        var session = _sessionRef;
        if (session != null) {
            foreach (var unit in session.Units) {
                if (unit.UnitId == netId)
                    return unit.UnitName;
            }
        }
        return $"#{netId}";
    }

    /// <summary>按技能强类型 ID 解析技能名；资源表未注册回退为裸 ID。</summary>
    private static string ResolveSkillName(SkillKeyId skillId)
        => SkillResourceTable.GetResourceBySkillId(skillId)?.SkillName ?? $"技能 {skillId.Id}";

    /// <summary>按 Buff 类型 ID 解析 Buff 名；资源表未注册回退为裸 ID。</summary>
    private static string ResolveBuffName(ushort buffTypeId)
        => _buffTable.Value?.GetResourceByBuffTypeId(buffTypeId)?.BuffName ?? $"Buff {buffTypeId}";
}
