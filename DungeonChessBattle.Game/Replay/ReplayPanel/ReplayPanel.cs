using System;
using System.Collections.Generic;
using DungeonChessBattle.Game.GamePanels;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using MainSceneNode = DungeonChessBattle.MainScene.MainScene;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// 回放入口面板：呈现由回放浏览服务裁决好的行视图列表，逐行卡片，下载与播放各自独立。
/// 本面板不保存过程状态（列表、在途与进度皆在 ReplayService），_Process 每帧消费行视图结论渲染。
/// 每行有下载按钮（只获取）与播放按钮（显式启动回放）。
/// 前厅页面之一，打开与返回走 BaseGamePanel 导航链；屏幕态交 ReplayCoordinator 信号仲裁。
/// 回放组装场景互斥加载，播放请求经导出的 <see cref="MainSceneNode"/> 装配启动，本面板不接触回放引擎与编排器。
/// </summary>
public partial class ReplayPanel : BaseGamePanel {
    private static readonly ILogger<ReplayPanel> _logger = ServiceLocator.GetLogger<ReplayPanel>();

    /// <summary>主场景装配根：回放组装场景互斥加载，启动请求交其实例化并驱动。</summary>
    [Export]
    private MainSceneNode? _assembler;

    /// <summary>导出引用集合节点。</summary>
    public ReplayPanelInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>房间 ID 到条目卡片的缓存（视图所有权，非会话状态）。</summary>
    private readonly Dictionary<string, ReplayItem> _rows = [];

    /// <summary>节点就绪：获取引用集合，绑定按钮。</summary>
    public override void _Ready() {
        InterRefs = GetNode<ReplayPanelInterRefs>("ReplayPanelInterRefs");
        if (InterRefs is null) {
            _logger.LogError("ReplayPanelInterRefs node not found.");
            return;
        }
        InterRefs.RefreshButton?.Pressed += OnRefreshPressed;
        InterRefs.CloseButton?.Pressed += GoBack;
    }

    /// <summary>面板打开：取一次合并列表。会话失效的在途清理由客户端状态机经 ReplayService.OnSessionInvalid 处理。</summary>
    protected override void OnPanelOpened() => Refresh();

    /// <summary>每帧取最新行视图并增量刷新；面板隐藏时不消费。</summary>
    public override void _Process(double delta) {
        if (!Visible || !IsInstanceValid(this))
            return;
        RenderRows(ServiceLocator.ReplayService.GetRowViews());
    }

    /// <summary>取一次合并列表，结果写服务状态，由 _Process 回填。</summary>
    private static void Refresh() => ServiceLocator.ReplayService.RefreshList();

    /// <summary>刷新按钮回调。</summary>
    private void OnRefreshPressed() => Refresh();

    /// <summary>下载按钮回调：可下载与否由服务层裁决，本面板只上报房间 ID。</summary>
    private void OnRowActionPressed(string roomId) => ServiceLocator.ReplayService.RequestFetch(roomId);

    /// <summary>播放按钮回调：服务层取可重放记录，成功即交主场景装配回放场景启动并返回大厅。</summary>
    private void OnPlayPressed(string roomId) {
        var result = ServiceLocator.ReplayService.TryGetPlayable(roomId);
        if (!result.IsReady || result.Recording is null) {
            if (IsInstanceValid(this))
                _logger.LogWarning("回放本地副本无法读取或版本不兼容，无法启动: {RoomId}（{Status}）", roomId, result.Status);
            return;
        }
        if (_assembler?.StartReplay(result.Recording) == true)
            GoBack();
    }

    /// <summary>按行视图增量刷新逐行卡片：摘要随回放元数据固定仅新建构建，动作态每帧更新。</summary>
    private void RenderRows(IReadOnlyList<ReplayRowView> views) {
        var container = InterRefs?.ReplayListContainer;
        if (container == null)
            return;

        var currentRoomIds = new HashSet<string>();
        foreach (var view in views)
            currentRoomIds.Add(view.RoomId);

        var stale = new List<string>();
        foreach (var roomId in _rows.Keys)
            if (!currentRoomIds.Contains(roomId))
                stale.Add(roomId);
        foreach (var roomId in stale) {
            if (!_rows.Remove(roomId, out var row))
                continue;
            container.RemoveChild(row);
            row.QueueFree();
        }

        foreach (var view in views) {
            if (!_rows.TryGetValue(view.RoomId, out var row)) {
                row = CreateRow();
                container.AddChild(row);
                _rows[view.RoomId] = row;
                row.UpdateSummary(view);
            }
            row.SetAction(view.Action, view.PlayEnabled, view.DownloadPercent);
        }
    }

    /// <summary>实例化单条回放卡片并订阅其行按钮信号。</summary>
    private ReplayItem CreateRow() {
        if (InterRefs?.ReplayItemScene is null)
            throw new InvalidOperationException("ReplayItemScene is not assigned.");
        var row = InterRefs.ReplayItemScene.Instantiate<ReplayItem>();
        row.ActionPressed += OnRowActionPressed;
        row.PlayPressed += OnPlayPressed;
        return row;
    }
}
