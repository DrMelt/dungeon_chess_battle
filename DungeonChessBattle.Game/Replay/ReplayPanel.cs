using System;
using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.ReplayUI;

/// <summary>
/// 回放入口面板：查询当前登录玩家的回放列表，选择并下载后启动回放场景。
/// 下载经 HTTP 端点凭一次性凭证获取回放字节流，交由 ReplayCoordinator 播放。
/// 通过导出引用绑定 UI 控件与回放场景。
/// </summary>
public partial class ReplayPanel : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayPanel> _logger = ServiceLocator.GetLogger<ReplayPanel>();

    /// <summary>回放摘要列表控件。</summary>
    [Export]
    private ItemList? _replayList;

    /// <summary>回放下载 HTTP 请求节点。</summary>
    [Export]
    private HttpRequest? _downloadRequest;

    /// <summary>回放场景根节点（Node3D 后代），启动后显示。</summary>
    [Export]
    private Node3D? _replayScene;

    /// <summary>回放场景编排器。</summary>
    [Export]
    private ReplayCoordinator? _coordinator;

    private System.Collections.Generic.IReadOnlyList<ReplaySummaryDto> _replays = [];
    private string? _pendingRoomId;

    /// <summary>节点就绪：订阅回放事件。</summary>
    public override void _Ready() {
        ServiceLocator.ClientService.OnReplayListReceived += OnReplayListReceived;
        ServiceLocator.ClientService.OnReplayDownloadResult += OnReplayDownloadResult;
        _downloadRequest?.RequestCompleted += OnDownloadCompleted;
    }

    /// <summary>刷新回放列表。</summary>
    public static void Refresh() {
        ServiceLocator.ClientService.RequestGetReplays();
    }

    /// <summary>播放选中回放。</summary>
    public void OnPlayPressed() {
        var selected = _replayList?.GetSelectedItems();
        if (selected == null || selected.Length == 0) {
            _logger.LogWarning("未选择回放");
            return;
        }
        int index = selected[0];
        if (index < 0 || index >= _replays.Count)
            return;
        _pendingRoomId = _replays[index].RoomId;
        ServiceLocator.ClientService.RequestDownloadReplay(_pendingRoomId);
    }

    private void OnReplayListReceived(System.Collections.Generic.IReadOnlyList<ReplaySummaryDto> replays) {
        _replays = replays;
        var list = _replayList;
        if (list == null)
            return;
        list.Clear();
        foreach (var replay in replays) {
            var time = DateTimeOffset.FromUnixTimeSeconds(replay.StartUnixTime).ToLocalTime().ToString("MM-dd HH:mm");
            list.AddItem($"{replay.DungeonKey}  {time}");
        }
    }

    private void OnReplayDownloadResult(ReplayDownloadResult result) {
        if (!result.Success || result.DownloadTicket == null || result.RoomId != _pendingRoomId) {
            if (!result.Success)
                _logger.LogWarning("回放下载失败：{Error}", result.Error ?? "unknown");
            return;
        }
        var url = ServiceLocator.ClientService.GetReplayDownloadUrl(result.RoomId, result.DownloadTicket);
        _downloadRequest?.Request(url);
    }

    private void OnDownloadCompleted(long result, long responseCode, string[] headers, byte[] body) {
        if (result != (long)HttpRequest.Result.Success || responseCode != 200) {
            _logger.LogWarning("回放下载请求失败：result={Result}, code={Code}", result, responseCode);
            return;
        }
        if (_coordinator == null || _replayScene == null) {
            _logger.LogError("回放场景未绑定，无法启动回放");
            return;
        }
        _coordinator.LoadReplay(body);
        _replayScene.Visible = true;
        Visible = false;
    }

    /// <summary>节点退出：退订事件。</summary>
    public override void _ExitTree() {
        ServiceLocator.ClientService.OnReplayListReceived -= OnReplayListReceived;
        ServiceLocator.ClientService.OnReplayDownloadResult -= OnReplayDownloadResult;
        _downloadRequest?.RequestCompleted -= OnDownloadCompleted;
    }
}
