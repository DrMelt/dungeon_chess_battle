using System;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Mod;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// 回放列表条目卡片：左侧摘要文本，右侧动作按钮。
/// 卡片只上报按钮点击并呈现行视图结论（文案与可用态），动作语义由 ReplayService 裁决。
/// 使用 InterRefs 模式分离 [Export] 引用。
/// </summary>
public partial class ReplayItem : Control {
    /// <summary>行按钮点击信号，参数为条目归属的房间 ID。</summary>
    [Signal]
    public delegate void ActionPressedEventHandler(string roomId);

    /// <summary>播放按钮点击信号，参数为条目归属的房间 ID。</summary>
    [Signal]
    public delegate void PlayPressedEventHandler(string roomId);

    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayItem> _logger = ServiceLocator.GetLogger<ReplayItem>();

    /// <summary>导出引用集合节点。</summary>
    private ReplayItemInterRefs? _refs;

    /// <summary>当前条目归属的房间 ID。</summary>
    public string RoomId { get; private set; } = "";

    /// <summary>仅存本地副本（服务端已无归档）时的摘要后缀。</summary>
    private const string LocalOnlySuffix = "  仅本地";

    /// <summary>节点就绪：获取引用集合并绑定行按钮。</summary>
    public override void _Ready() {
        _refs = GetNode<ReplayItemInterRefs>("ReplayItemInterRefs");
        if (_refs is null) {
            _logger.LogError("ReplayItemInterRefs node not found.");
            return;
        }

        _refs.ActionButton?.Pressed += OnActionButtonPressed;
        _refs.PlayButton?.Pressed += OnPlayButtonPressed;
    }

    /// <summary>按视图动作语义刷新动作按钮文案与可用态；下载进度在任务派生时带上。</summary>
    public void SetAction(ReplayBrowseAction action, bool playEnabled, int? downloadPercent) {
        if (_refs?.ActionButton != null) {
            _refs.ActionButton.Text = ActionText(action, downloadPercent);
            _refs.ActionButton.Disabled = action != ReplayBrowseAction.Download;
        }
        if (_refs?.PlayButton != null)
            _refs.PlayButton.Disabled = !playEnabled;
    }

    private static string ActionText(ReplayBrowseAction action, int? downloadPercent) => action switch {
        ReplayBrowseAction.Blocked => "不可回放",
        ReplayBrowseAction.Downloading => downloadPercent is { } p ? $"获取中 {p}%" : "获取中",
        ReplayBrowseAction.Download => "下载",
        _ => "",
    };

    /// <summary>刷新条目摘要：副本显示名、开始时间、时长、参与玩家与是否仅存本地。</summary>
    public void UpdateSummary(ReplayRowView view) {
        RoomId = view.RoomId;
        if (_refs?.InfoLabel == null)
            return;

        var dungeon = ModAssets.Dungeon(view.DungeonKey)?.DisplayName ?? view.DungeonKey;
        var time = DateTimeOffset.FromUnixTimeSeconds(view.StartUnixTime).ToLocalTime().ToString("MM-dd HH:mm");
        var players = string.Join("、", view.PlayerNames);
        // 服务端还有归档时随时可重下；只剩本地副本就标出来，删了这个文件就没有第二次
        var localOnly = view.FromServer ? "" : LocalOnlySuffix;
        _refs.InfoLabel.SetText($"{dungeon}  {time}  {DurationText(view)}  {players}{localOnly}");
    }

    /// <summary>回放时长文本，由帧数与 tick 频率换算；频率缺失时不留空白字段。</summary>
    private static string DurationText(ReplayRowView view) {
        if (view.TickRate <= 0 || view.DurationTicks <= 0)
            return "--:--";
        int seconds = view.DurationTicks / view.TickRate;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    /// <summary>行按钮点击：以当前房间 ID 上报面板。</summary>
    private void OnActionButtonPressed() => EmitSignal(SignalName.ActionPressed, RoomId);

    /// <summary>播放按钮点击：以当前房间 ID 上报面板。</summary>
    private void OnPlayButtonPressed() => EmitSignal(SignalName.PlayPressed, RoomId);
}
