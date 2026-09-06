using System;
using System.Text;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Replay;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// 回放输入条目面板：以当前帧为中心呈现前后若干条输入条目，当前条目高亮。
/// 只读投影，不持有回放状态：每帧按引擎帧号在 <see cref="ReplayInputTimeline"/> 上定位当前条目，
/// 定位结果与时间轴都没变就不重建文本。上一条/下一条按钮把引擎跳到相邻条目的起始帧，窗口随帧号自动跟随。
/// UI 控件经 ReplayInputPanelInterRefs 绑定并在 _Ready 接线；显隐随 ReplayUI 容器起落，归 ReplayCoordinator。
/// </summary>
public partial class ReplayInputPanel : Control {
    private static readonly ILogger<ReplayInputPanel> _logger = ServiceLocator.GetLogger<ReplayInputPanel>();

    /// <summary>回放编排器引用，跨场景依赖，由 MainScene 注入。</summary>
    [Export]
    private ReplayCoordinator? _coordinator;

    /// <summary>导出引用集合节点。</summary>
    private ReplayInputPanelInterRefs? _refs;

    /// <summary>当前条目之前呈现的条目数。</summary>
    private const int BeforeLines = 5;

    /// <summary>当前条目之后呈现的条目数。</summary>
    private const int AfterLines = 10;

    /// <summary>当前条目行色。</summary>
    private const string CurrentColor = "#ffd479";
    /// <summary>已过条目行色。</summary>
    private const string PastColor = "#8a8a8a";
    /// <summary>未到条目行色。</summary>
    private const string FutureColor = "#e8e8e8";
    /// <summary>当前条目行首标记。</summary>
    private const string CurrentMarker = "▶ ";

    /// <summary>尚无条目覆盖当前帧的下标，早于全部条目时窗口整体落在未来。</summary>
    private const int NoEntry = -1;

    /// <summary>尚未呈现过任何窗口的下标哨兵，与 <see cref="NoEntry"/> 区分开：换回放时同一条目下标也要重绘。</summary>
    private const int Unrendered = -2;

    private ReplayInputTimeline? _seenTimeline;
    private int _seenIndex = Unrendered;

    /// <summary>节点就绪：获取引用集合并绑定条目跳转按钮。</summary>
    public override void _Ready() {
        _refs = GetNode<ReplayInputPanelInterRefs>("ReplayInputPanelInterRefs");
        if (_refs is null) {
            _logger.LogError("ReplayInputPanelInterRefs node not found.");
            return;
        }

        _refs.PrevButton?.Pressed += OnPrevPressed;
        _refs.NextButton?.Pressed += OnNextPressed;
    }

    /// <summary>每帧定位当前条目，变化时重建窗口文本，并按相邻条目刷新按钮可用态。</summary>
    public override void _Process(double delta) {
        var engine = _coordinator?.Engine;
        if (engine == null || _refs == null) {
            _seenTimeline = null;
            _seenIndex = Unrendered;
            return;
        }

        var timeline = engine.Inputs;
        int absolute = AbsoluteFrame(engine);
        int index = timeline.IndexOfFrameAtOrBefore(absolute);
        if (!ReferenceEquals(timeline, _seenTimeline) || index != _seenIndex) {
            _seenTimeline = timeline;
            _seenIndex = index;
            Render(engine, timeline, index);
        }

        if (_refs.PrevButton is { } prev)
            prev.Disabled = PrevIndex(timeline, index) < 0;
        if (_refs.NextButton is { } next)
            next.Disabled = timeline.IndexOfFrameAfter(absolute) < 0;
    }

    /// <summary>上一条按钮回调：跳到当前条目所在帧之前的最近一条输入。</summary>
    private void OnPrevPressed() {
        var coordinator = _coordinator;
        var engine = coordinator?.Engine;
        if (coordinator == null || engine == null)
            return;

        var timeline = engine.Inputs;
        int prev = PrevIndex(timeline, timeline.IndexOfFrameAtOrBefore(AbsoluteFrame(engine)));
        if (prev >= 0)
            coordinator.SeekToFrame(timeline.Entries[prev].Frame - engine.StartTick);
    }

    /// <summary>下一条按钮回调：跳到晚于当前帧的第一条输入。</summary>
    private void OnNextPressed() {
        var coordinator = _coordinator;
        var engine = coordinator?.Engine;
        if (coordinator == null || engine == null)
            return;

        var timeline = engine.Inputs;
        int next = timeline.IndexOfFrameAfter(AbsoluteFrame(engine));
        if (next >= 0)
            coordinator.SeekToFrame(timeline.Entries[next].Frame - engine.StartTick);
    }

    /// <summary>重建窗口文本：当前条目上下各取若干条，按与当前帧的先后着色。</summary>
    private void Render(ReplayEngine engine, ReplayInputTimeline timeline, int index) {
        var entries = timeline.Entries;
        int first = Math.Max(0, index - BeforeLines);
        int last = Math.Min(entries.Count - 1, index + AfterLines);
        var text = new StringBuilder();
        for (int i = first; i <= last; i++) {
            string color = ColorFor(i, index);
            string marker = i == index ? CurrentMarker : "  ";
            text.Append($"[color={color}]{marker}{Line(engine, entries[i])}[/color]\n");
        }

        if (_refs?.ListLabel is { } label) {
            label.Clear();
            label.AppendText(text.ToString());
        }
        if (_refs?.CounterLabel is { } counter)
            counter.Text = $"{index + 1}/{entries.Count}";
    }

    /// <summary>条目按与当前帧的先后着色。</summary>
    private static string ColorFor(int index, int currentIndex) {
        if (index < currentIndex)
            return PastColor;
        return index == currentIndex ? CurrentColor : FutureColor;
    }

    /// <summary>条目文本行：相对秒数、玩家名与按类型展开的输入内容。</summary>
    private static string Line(ReplayEngine engine, in ReplayInputEntry entry) {
        double seconds = (entry.Frame - engine.StartTick) * engine.FixedDelta;
        string body = entry.Kind switch {
            ReplayInputKind.Move => $"移动 ({entry.MoveDir.X:0.##}, {entry.MoveDir.Y:0.##}) ×{entry.Length}帧",
            ReplayInputKind.Cast => $"施法 {entry.SkillKey} → {TargetName(engine, entry.TargetNetId, "位置")} ({entry.TargetPos.X:0.##}, {entry.TargetPos.Y:0.##})",
            _ => $"聚焦 → {TargetName(engine, entry.TargetNetId, "无")}",
        };
        string head = $"[{seconds:0.0}s] {PlayerName(engine, entry.PlayerIndex)} {body}";
        return entry.Accepted ? head : head + " 未接管";
    }

    /// <summary>上一条条目下标：早于当前条目所在帧的最近一条，无当前条目即无解。</summary>
    private static int PrevIndex(ReplayInputTimeline timeline, int index) =>
        index == NoEntry ? -1 : timeline.IndexOfFrameAtOrBefore(timeline.Entries[index].Frame - 1);

    /// <summary>条目帧号所在的绝对逻辑帧，与归档同数轴。</summary>
    private static int AbsoluteFrame(ReplayEngine engine) => engine.StartTick + engine.Frame;

    /// <summary>玩家序号 → 玩家名，越界回退裸序号。</summary>
    private static string PlayerName(ReplayEngine engine, int playerIndex) {
        var players = engine.Players;
        return playerIndex < players.Count ? Safe(players[playerIndex].PlayerName) : $"#{playerIndex}";
    }

    /// <summary>目标名：0 按该类型的无目标口径显示，其余按当前世界解析，解析不到回退裸 ID。</summary>
    private static string TargetName(ReplayEngine engine, ushort netId, string noneLabel) {
        if (netId == 0)
            return noneLabel;
        return engine.FindUnit(netId) is { } unit ? Safe(unit.UnitName) : $"#{netId}";
    }

    /// <summary>换掉 bbcode 方括号：玩家名与技能键是外部输入，不能当标记解析。</summary>
    private static string Safe(string text) =>
        text.Contains('[') || text.Contains(']') ? text.Replace('[', '(').Replace(']', ')') : text;
}
